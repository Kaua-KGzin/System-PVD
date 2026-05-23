using Pdv.Backend.Domain;

namespace Pdv.Backend.Contracts;

public sealed record CreateProductRequest(
    string Barcode,
    string? Sku,
    string Name,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal StockQuantity,
    decimal MinStockQuantity,
    Guid? CategoryId = null,
    bool IsActive = true);

public sealed record UpdateProductRequest(
    string Barcode,
    string? Sku,
    string Name,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal MinStockQuantity,
    Guid? CategoryId,
    bool IsActive);

public sealed record AdjustStockRequest(decimal QuantityDelta, string? Reason);

public sealed record ProductResponse(
    Guid Id,
    string Barcode,
    string? Sku,
    string Name,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal StockQuantity,
    decimal MinStockQuantity,
    Guid? CategoryId,
    string? CategoryName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ProductResponse From(Product product) =>
        new(
            product.Id,
            product.Barcode,
            product.Sku,
            product.Name,
            product.UnitOfMeasure,
            product.UnitPrice,
            product.StockQuantity,
            product.MinStockQuantity,
            product.CategoryId,
            product.Category?.Name,
            product.IsActive,
            product.CreatedAt,
            product.UpdatedAt);
}
