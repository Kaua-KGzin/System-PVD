namespace Archlab.Backend.Domain;

public sealed class CashSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string TerminalId { get; set; }
    public required string OperatorName { get; set; }
    public decimal OpeningAmount { get; set; }
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal? ExpectedClosingAmount { get; set; }
    public decimal? ClosingAmount { get; set; }
    public decimal? ClosingDifference { get; set; }
    public string? ClosingNotes { get; set; }
    public CashSessionStatus Status { get; set; } = CashSessionStatus.Open;
    public uint RowVersion { get; set; }


    public List<Sale> Sales { get; set; } = [];
    public List<CashMovement> Movements { get; set; } = [];
}
