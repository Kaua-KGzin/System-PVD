namespace Archlab.Backend.Domain;

public sealed class SaleReturn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public required string Reason { get; set; }
    public required string OperatorName { get; set; }
    public decimal TotalRefundAmount { get; set; }
    public DateTimeOffset ReturnedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<SaleReturnItem> Items { get; set; } = [];
}
