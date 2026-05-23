namespace Archlab.Backend.Domain;

public sealed class PurchaseEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public required string InvoiceNumber { get; set; }
    public string? Notes { get; set; }
    public decimal TotalCost { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<PurchaseEntryItem> Items { get; set; } = [];
}

public sealed class PurchaseEntryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseEntryId { get; set; }
    public PurchaseEntry? PurchaseEntry { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}
