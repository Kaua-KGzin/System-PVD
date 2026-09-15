using System.Data;
using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Services;

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

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (DbConflict.IsRetryable(ex))
        {
            // "One open session per terminal" is held by the read above inside a Serializable
            // transaction, not by a unique index. On PostgreSQL two simultaneous openings both
            // pass that read and one is aborted at commit — which is the same answer the read
            // gives, so it gets the same message rather than a 500.
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<CashSessionResponse>.Fail("Este terminal ja possui um caixa aberto.", StatusCodes.Status409Conflict);
        }

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

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var expectedClosing = await CalculateExpectedClosingAsync(session, cancellationToken);
        session.ExpectedClosingAmount = expectedClosing;
        session.ClosingAmount = request.ClosingAmount;
        session.ClosingDifference = request.ClosingAmount - expectedClosing;
        session.ClosingNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        session.ClosedAt = DateTimeOffset.UtcNow;
        session.Status = CashSessionStatus.Closed;
        session.RowVersion++;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<CashSessionResponse>.Fail("Conflito de concorrencia ao fechar o caixa. Tente novamente.", StatusCodes.Status409Conflict);
        }
        catch (Exception ex) when (DbConflict.IsRetryable(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<CashSessionResponse>.Fail("Conflito de concorrencia ao fechar o caixa. Tente novamente.", StatusCodes.Status409Conflict);
        }

        return ServiceResult<CashSessionResponse>.Ok(CashSessionResponse.From(session, expectedClosing));
    }


    public async Task<ServiceResult<CashMovementResponse>> AddMovementAsync(
        Guid sessionId,
        CreateCashMovementRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
            return ServiceResult<CashMovementResponse>.Fail("Valor da movimentacao precisa ser maior que zero.");

        if (string.IsNullOrWhiteSpace(request.OperatorName))
            return ServiceResult<CashMovementResponse>.Fail("Operador e obrigatorio.");

        var session = await db.CashSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
            return ServiceResult<CashMovementResponse>.Fail("Sessao de caixa nao encontrada.", StatusCodes.Status404NotFound);

        if (session.Status != CashSessionStatus.Open)
            return ServiceResult<CashMovementResponse>.Fail("Movimentacoes so podem ser registradas em um caixa aberto.", StatusCodes.Status409Conflict);

        var movement = new CashMovement
        {
            CashSessionId = session.Id,
            Type = request.Type,
            Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            OperatorName = request.OperatorName.Trim()
        };

        db.CashMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<CashMovementResponse>.Ok(CashMovementResponse.From(movement));
    }

    public async Task<CashMovementResponse[]> ListMovementsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await db.CashMovements.AsNoTracking()
            .Where(m => m.CashSessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => CashMovementResponse.From(m))
            .ToArrayAsync(cancellationToken);
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

        var supplies = await db.CashMovements.AsNoTracking()
            .Where(m => m.CashSessionId == session.Id && m.Type == CashMovementType.Supply)
            .SumAsync(m => m.Amount, cancellationToken);

        var bleeds = await db.CashMovements.AsNoTracking()
            .Where(m => m.CashSessionId == session.Id && m.Type == CashMovementType.Bleed)
            .SumAsync(m => m.Amount, cancellationToken);

        return session.OpeningAmount + cashReceived - changePaid + supplies - bleeds;
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

        var suppliesBySession = await db.CashMovements.AsNoTracking()
            .Where(m => sessionIds.Contains(m.CashSessionId) && m.Type == CashMovementType.Supply)
            .GroupBy(m => m.CashSessionId)
            .Select(g => new { CashSessionId = g.Key, Total = g.Sum(m => m.Amount) })
            .ToDictionaryAsync(x => x.CashSessionId, x => x.Total, cancellationToken);

        var bleedsBySession = await db.CashMovements.AsNoTracking()
            .Where(m => sessionIds.Contains(m.CashSessionId) && m.Type == CashMovementType.Bleed)
            .GroupBy(m => m.CashSessionId)
            .Select(g => new { CashSessionId = g.Key, Total = g.Sum(m => m.Amount) })
            .ToDictionaryAsync(x => x.CashSessionId, x => x.Total, cancellationToken);

        return sessions.ToDictionary(
            s => s.Id,
            s => s.OpeningAmount
                + cashReceivedBySession.GetValueOrDefault(s.Id, 0m)
                - changePaidBySession.GetValueOrDefault(s.Id, 0m)
                + suppliesBySession.GetValueOrDefault(s.Id, 0m)
                - bleedsBySession.GetValueOrDefault(s.Id, 0m));
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
