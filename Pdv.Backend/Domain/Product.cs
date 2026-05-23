namespace Pdv.Backend.Domain;

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Barcode { get; set; }
    public string? Sku { get; set; }
    public required string Name { get; set; }
    public string UnitOfMeasure { get; set; } = "UN";
    public decimal UnitPrice { get; set; }
    public decimal StockQuantity { get; set; }
    public decimal MinStockQuantity { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<InventoryMovement> InventoryMovements { get; set; } = [];
}
