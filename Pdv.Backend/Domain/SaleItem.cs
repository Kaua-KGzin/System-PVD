namespace Pdv.Backend.Domain;

public sealed class SaleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleId { get; set; }
    public Sale? Sale { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public required string Barcode { get; set; }
    public required string ProductName { get; set; }
    public required string UnitOfMeasure { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitDiscount { get; set; }
    public decimal GrossTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal NetTotal { get; set; }
}
