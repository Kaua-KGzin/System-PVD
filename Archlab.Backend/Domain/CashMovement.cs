namespace Archlab.Backend.Domain;

public sealed class CashMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CashSessionId { get; set; }
    public CashSession? CashSession { get; set; }
    public CashMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public required string OperatorName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
