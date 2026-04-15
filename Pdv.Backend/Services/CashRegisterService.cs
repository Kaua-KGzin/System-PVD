using Microsoft.EntityFrameworkCore;
using Pdv.Backend.Contracts;
using Pdv.Backend.Data;
using Pdv.Backend.Domain;

namespace Pdv.Backend.Services;

public sealed class CashRegisterService(PdvDbContext db)
{
    public async Task<IReadOnlyList<CashSessionResponse>> ListAsync(
        string? terminalId,
        bool onlyOpen,
        CancellationToken cancellationToken)
    {
        var query = db.CashSessions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(terminalId))
        {
            var normalizedTerminalId = Normalize(terminalId);
            query = query.Where(session => session.TerminalId == normalizedTerminalId);
        }

        if (onlyOpen)
        {
            query = query.Where(session => session.Status == CashSessionStatus.Open);
        }

        var sessions = await query
            .OrderByDescending(session => session.OpenedAt)
            .ToArrayAsync(cancellationToken);

        var responses = new List<CashSessionResponse>(sessions.Length);
        foreach (var session in sessions)
        {
            responses.Add(CashSessionResponse.From(session, await CalculateExpectedClosingAsync(session.Id, cancellationToken)));
        }

        return responses;
    }

    public async Task<ServiceResult<CashSessionResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await db.CashSessions.AsNoTracking()
            .FirstOrDefaultAsync(session => session.Id == id, cancellationToken);

        if (session is null)
        {
            return ServiceResult<CashSessionResponse>.Fail("Sessao de caixa nao encontrada.", StatusCodes.Status404NotFound);
        }

        var expectedClosing = await CalculateExpectedClosingAsync(session.Id, cancellationToken);
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
        {
            return ServiceResult<CashSessionResponse>.Fail("Nao ha caixa aberto para este terminal.", StatusCodes.Status404NotFound);
        }

        var expectedClosing = await CalculateExpectedClosingAsync(session.Id, cancellationToken);
        return ServiceResult<CashSessionResponse>.Ok(CashSessionResponse.From(session, expectedClosing));
    }

    public async Task<ServiceResult<CashSessionResponse>> OpenAsync(OpenCashSessionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TerminalId))
        {
            return ServiceResult<CashSessionResponse>.Fail("Terminal do PDV e obrigatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.OperatorName))
        {
            return ServiceResult<CashSessionResponse>.Fail("Operador do caixa e obrigatorio.");
        }

        if (request.OpeningAmount < 0)
        {
            return ServiceResult<CashSessionResponse>.Fail("Valor de abertura nao pode ser negativo.");
        }

        var terminalId = Normalize(request.TerminalId);
        var hasOpenSession = await db.CashSessions.AnyAsync(
            session => session.TerminalId == terminalId && session.Status == CashSessionStatus.Open,
            cancellationToken);

        if (hasOpenSession)
        {
            return ServiceResult<CashSessionResponse>.Fail("Este terminal ja possui um caixa aberto.", StatusCodes.Status409Conflict);
        }

        var session = new CashSession
        {
            TerminalId = terminalId,
            OperatorName = request.OperatorName.Trim(),
            OpeningAmount = request.OpeningAmount
        };

        db.CashSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<CashSessionResponse>.Ok(CashSessionResponse.From(session, session.OpeningAmount));
    }

    public async Task<ServiceResult<CashSessionResponse>> CloseAsync(Guid id, CloseCashSessionRequest request, CancellationToken cancellationToken)
    {
        if (request.ClosingAmount < 0)
        {
            return ServiceResult<CashSessionResponse>.Fail("Valor de fechamento nao pode ser negativo.");
        }

        var session = await db.CashSessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);
        if (session is null)
        {
            return ServiceResult<CashSessionResponse>.Fail("Sessao de caixa nao encontrada.", StatusCodes.Status404NotFound);
        }

        if (session.Status == CashSessionStatus.Closed)
        {
            return ServiceResult<CashSessionResponse>.Fail("Esta sessao de caixa ja esta fechada.", StatusCodes.Status409Conflict);
        }

        var expectedClosing = await CalculateExpectedClosingAsync(session.Id, cancellationToken);
        session.ExpectedClosingAmount = expectedClosing;
        session.ClosingAmount = request.ClosingAmount;
        session.ClosingDifference = request.ClosingAmount - expectedClosing;
        session.ClosingNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        session.ClosedAt = DateTimeOffset.UtcNow;
        session.Status = CashSessionStatus.Closed;

        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<CashSessionResponse>.Ok(CashSessionResponse.From(session, expectedClosing));
    }

    private async Task<decimal> CalculateExpectedClosingAsync(Guid cashSessionId, CancellationToken cancellationToken)
    {
        var session = await db.CashSessions.AsNoTracking()
            .FirstAsync(session => session.Id == cashSessionId, cancellationToken);

        var sales = await db.Sales.AsNoTracking()
            .Include(sale => sale.Payments)
            .Where(sale => sale.CashSessionId == cashSessionId && sale.Status == SaleStatus.Completed)
            .ToArrayAsync(cancellationToken);

        var cashReceived = sales
            .SelectMany(sale => sale.Payments)
            .Where(payment => payment.Method == PaymentMethod.Cash)
            .Sum(payment => payment.Amount);

        var changePaid = sales.Sum(sale => sale.ChangeAmount);

        return session.OpeningAmount + cashReceived - changePaid;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
