namespace Archlab.Backend.Domain;

public sealed class SaleReturnItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleReturnId { get; set; }
    public SaleReturn? SaleReturn { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public required string Barcode { get; set; }
    public required string ProductName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal RefundAmount { get; set; }
}
