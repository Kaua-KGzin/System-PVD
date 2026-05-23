using System.Data;
using Microsoft.EntityFrameworkCore;
using Pdv.Backend.Contracts;
using Pdv.Backend.Data;
using Pdv.Backend.Domain;

namespace Pdv.Backend.Services;

public sealed class CashRegisterService(PdvDbContext db)
{
    public async Task<PagedResponse<CashSessionResponse>> ListAsync(
        string? terminalId,
        bool onlyOpen,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.CashSessions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(terminalId))
        {
            var normalizedTerminalId = Normalize(terminalId);
            query = query.Where(session => session.TerminalId == normalizedTerminalId);
        }

        if (onlyOpen)
            query = query.Where(session => session.Status == CashSessionStatus.Open);

        var totalCount = await query.CountAsync(cancellationToken);

        var sessions = await query
            .OrderByDescending(session => session.OpenedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        var sessionIds = sessions.Select(s => s.Id).ToArray();
        var expectedClosings = await ComputeExpectedClosingsAsync(sessions, sessionIds, cancellationToken);

        var items = sessions
            .Select(s => CashSessionResponse.From(s, expectedClosings.GetValueOrDefault(s.Id, s.OpeningAmount)))
            .ToArray();

        return new PagedResponse<CashSessionResponse>(items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ServiceResult<CashSessionResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await db.CashSessions.AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

        if (session is null)
            return ServiceResult<CashSessionResponse>.Fail("Sessao de caixa nao encontrada.", StatusCodes.Status404NotFound);

        var expectedClosing = await CalculateExpectedClosingAsync(session, cancellationToken);
        return ServiceResult<CashSessionResponse>.Ok(CashSessionResponse.From(session, expectedClosing));
    }

    public async Task<ServiceResult<CashSessionResponse>> GetOpenByTerminalAsync(string terminalId, CancellationToken cancellationToken)
    {
        var normalizedTerminalId = Normalize(terminalId);
        var session = await db.CashSessions.AsNoTracking()
            .FirstOrDefaultAsync(
                session => session.TerminalId == normalizedTerminalId && session.Status == CashSessionStatus.Open,
                cancellationToken);

        if (session is null)
            return ServiceResult<CashSessionResponse>.Fail("Nao ha caixa aberto para este terminal.", StatusCodes.Status404NotFound);

        var expectedClosing = await CalculateExpectedClosingAsync(session, cancellationToken);
        return ServiceResult<CashSessionResponse>.Ok(CashSessionResponse.From(session, expectedClosing));
    }

    public async Task<ServiceResult<CashSessionResponse>> OpenAsync(OpenCashSessionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TerminalId))
            return ServiceResult<CashSessionResponse>.Fail("Terminal do PDV e obrigatorio.");

        if (string.IsNullOrWhiteSpace(request.OperatorName))
            return ServiceResult<CashSessionResponse>.Fail("Operador do caixa e obrigatorio.");

        if (request.OpeningAmount < 0)
            return ServiceResult<CashSessionResponse>.Fail("Valor de abertura nao pode ser negativo.");

        var terminalId = Normalize(request.TerminalId);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var hasOpenSession = await db.CashSessions.AnyAsync(
            session => session.TerminalId == terminalId && session.Status == CashSessionStatus.Open,
            cancellationToken);

        if (hasOpenSession)
            return ServiceResult<CashSessionResponse>.Fail("Este terminal ja possui um caixa aberto.", StatusCodes.Status409Conflict);

        var session = new CashSession
        {
            TerminalId = terminalId,
            OperatorName = request.OperatorName.Trim(),
            OpeningAmount = request.OpeningAmount
        };

        db.CashSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<CashSessionResponse>.Ok(CashSessionResponse.From(session, session.OpeningAmount));
    }

    public async Task<ServiceResult<CashSessionResponse>> CloseAsync(Guid id, CloseCashSessionRequest request, CancellationToken cancellationToken)
    {
        if (request.ClosingAmount < 0)
            return ServiceResult<CashSessionResponse>.Fail("Valor de fechamento nao pode ser negativo.");

        var session = await db.CashSessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);
        if (session is null)
            return ServiceResult<CashSessionResponse>.Fail("Sessao de caixa nao encontrada.", StatusCodes.Status404NotFound);

        if (session.Status == CashSessionStatus.Closed)
            return ServiceResult<CashSessionResponse>.Fail("Esta sessao de caixa ja esta fechada.", StatusCodes.Status409Conflict);

        var expectedClosing = await CalculateExpectedClosingAsync(session, cancellationToken);
        session.ExpectedClosingAmount = expectedClosing;
        session.ClosingAmount = request.ClosingAmount;
        session.ClosingDifference = request.ClosingAmount - expectedClosing;
        session.ClosingNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        session.ClosedAt = DateTimeOffset.UtcNow;
        session.Status = CashSessionStatus.Closed;

        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<CashSessionResponse>.Ok(CashSessionResponse.From(session, expectedClosing));
    }

    private async Task<decimal> CalculateExpectedClosingAsync(CashSession session, CancellationToken cancellationToken)
    {
        var cashReceived = await db.SalePayments.AsNoTracking()
            .Where(p => p.Method == PaymentMethod.Cash &&
                        p.Sale!.CashSessionId == session.Id &&
                        p.Sale.Status == SaleStatus.Completed)
            .SumAsync(p => p.Amount, cancellationToken);

        var changePaid = await db.Sales.AsNoTracking()
            .Where(s => s.CashSessionId == session.Id && s.Status == SaleStatus.Completed)
            .SumAsync(s => s.ChangeAmount, cancellationToken);

        return session.OpeningAmount + cashReceived - changePaid;
    }

    private async Task<Dictionary<Guid, decimal>> ComputeExpectedClosingsAsync(
        CashSession[] sessions,
        Guid[] sessionIds,
        CancellationToken cancellationToken)
    {
        var cashReceivedBySession = await db.SalePayments.AsNoTracking()
            .Where(p => p.Method == PaymentMethod.Cash &&
                        sessionIds.Contains(p.Sale!.CashSessionId) &&
                        p.Sale.Status == SaleStatus.Completed)
            .GroupBy(p => p.Sale!.CashSessionId)
            .Select(g => new { CashSessionId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.CashSessionId, x => x.Total, cancellationToken);

        var changePaidBySession = await db.Sales.AsNoTracking()
            .Where(s => sessionIds.Contains(s.CashSessionId) && s.Status == SaleStatus.Completed)
            .GroupBy(s => s.CashSessionId)
            .Select(g => new { CashSessionId = g.Key, Total = g.Sum(s => s.ChangeAmount) })
            .ToDictionaryAsync(x => x.CashSessionId, x => x.Total, cancellationToken);

        return sessions.ToDictionary(
            s => s.Id,
            s => s.OpeningAmount
                + cashReceivedBySession.GetValueOrDefault(s.Id, 0m)
                - changePaidBySession.GetValueOrDefault(s.Id, 0m));
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
