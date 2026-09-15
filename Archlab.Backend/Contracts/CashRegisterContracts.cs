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

public sealed record CreateCashMovementRequest(
    CashMovementType Type,
    decimal Amount,
    string? Reason,
    string OperatorName);

public sealed record CashMovementResponse(
    Guid Id,
    Guid CashSessionId,
    CashMovementType Type,
    decimal Amount,
    string? Reason,
    string OperatorName,
    DateTimeOffset CreatedAt)
{
    public static CashMovementResponse From(CashMovement movement) =>
        new(
            movement.Id,
            movement.CashSessionId,
            movement.Type,
            movement.Amount,
            movement.Reason,
            movement.OperatorName,
            movement.CreatedAt);
}
