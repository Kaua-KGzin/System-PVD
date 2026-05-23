using Archlab.Backend.Domain;

namespace Archlab.Backend.Contracts;

public sealed record OpenCashSessionRequest(
    string TerminalId,
    string OperatorName,
    decimal OpeningAmount);

public sealed record CloseCashSessionRequest(
    decimal ClosingAmount,
    string? Notes);

public sealed record CashSessionResponse(
    Guid Id,
    string TerminalId,
    string OperatorName,
    decimal OpeningAmount,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal ExpectedClosingAmount,
    decimal? ClosingAmount,
    decimal? ClosingDifference,
    string? ClosingNotes,
    CashSessionStatus Status)
{
    public static CashSessionResponse From(CashSession session, decimal expectedClosingAmount) =>
        new(
            session.Id,
            session.TerminalId,
            session.OperatorName,
            session.OpeningAmount,
            session.OpenedAt,
            session.ClosedAt,
            expectedClosingAmount,
            session.ClosingAmount,
            session.ClosingDifference,
            session.ClosingNotes,
            session.Status);
}
