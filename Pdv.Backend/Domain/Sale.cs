namespace Pdv.Backend.Domain;

public sealed class Sale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public Guid CashSessionId { get; set; }
    public CashSession? CashSession { get; set; }
    public required string TerminalId { get; set; }
    public required string OperatorName { get; set; }
    public string? CustomerDocument { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public decimal GrossTotal { get; set; }
    public decimal ItemDiscountTotal { get; set; }
    public decimal SaleDiscountTotal { get; set; }
    public decimal NetTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public List<SaleItem> Items { get; set; } = [];
    public List<SalePayment> Payments { get; set; } = [];
    public FiscalDocument? FiscalDocument { get; set; }
}
