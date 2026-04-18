using Microsoft.EntityFrameworkCore;
using Pdv.Backend.Contracts;
using Pdv.Backend.Data;
using Pdv.Backend.Domain;

namespace Pdv.Backend.Services;

public sealed class ProductService(PdvDbContext db)
{
    public async Task<PagedResponse<ProductResponse>> SearchAsync(
        string? search,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Products.AsNoTracking();

        if (!includeInactive)
            query = query.Where(product => product.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(product =>
                product.Barcode.Contains(term) ||
                product.Name.Contains(term) ||
                (product.Sku != null && product.Sku.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(product => product.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => ProductResponse.From(product))
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<ProductResponse>(items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ServiceResult<ProductResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

        return product is null
            ? ServiceResult<ProductResponse>.Fail("Produto nao encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<ProductResponse>.Ok(ProductResponse.From(product));
    }

    public async Task<ServiceResult<ProductResponse>> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        var normalizedBarcode = Normalize(barcode);
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(product => product.Barcode == normalizedBarcode, cancellationToken);

        return product is null
            ? ServiceResult<ProductResponse>.Fail("Produto nao encontrado para o codigo de barras informado.", StatusCodes.Status404NotFound)
            : ServiceResult<ProductResponse>.Ok(ProductResponse.From(product));
    }

    public async Task<ServiceResult<ProductResponse>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateProduct(request.Barcode, request.Name, request.UnitOfMeasure, request.UnitPrice, request.StockQuantity, request.MinStockQuantity);
        if (validation is not null)
            return ServiceResult<ProductResponse>.Fail(validation);

        var barcode = Normalize(request.Barcode);
        var sku = NormalizeNullable(request.Sku);

        if (await db.Products.AnyAsync(product => product.Barcode == barcode, cancellationToken))
            return ServiceResult<ProductResponse>.Fail("Ja existe um produto com este codigo de barras.", StatusCodes.Status409Conflict);

        if (sku is not null && await db.Products.AnyAsync(product => product.Sku == sku, cancellationToken))
            return ServiceResult<ProductResponse>.Fail("Ja existe um produto com este SKU.", StatusCodes.Status409Conflict);

        var product = new Product
        {
            Barcode = barcode,
            Sku = sku,
            Name = request.Name.Trim(),
            UnitOfMeasure = request.UnitOfMeasure.Trim().ToUpperInvariant(),
            UnitPrice = request.UnitPrice,
            StockQuantity = request.StockQuantity,
            MinStockQuantity = request.MinStockQuantity,
            IsActive = request.IsActive
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductResponse>.Ok(ProductResponse.From(product));
    }

    public async Task<ServiceResult<ProductResponse>> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateProduct(request.Barcode, request.Name, request.UnitOfMeasure, request.UnitPrice, 0, request.MinStockQuantity);
        if (validation is not null)
            return ServiceResult<ProductResponse>.Fail(validation);

        var product = await db.Products.FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
        if (product is null)
            return ServiceResult<ProductResponse>.Fail("Produto nao encontrado.", StatusCodes.Status404NotFound);

        var barcode = Normalize(request.Barcode);
        var sku = NormalizeNullable(request.Sku);

        if (await db.Products.AnyAsync(other => other.Id != id && other.Barcode == barcode, cancellationToken))
            return ServiceResult<ProductResponse>.Fail("Ja existe outro produto com este codigo de barras.", StatusCodes.Status409Conflict);

        if (sku is not null && await db.Products.AnyAsync(other => other.Id != id && other.Sku == sku, cancellationToken))
            return ServiceResult<ProductResponse>.Fail("Ja existe outro produto com este SKU.", StatusCodes.Status409Conflict);

        product.Barcode = barcode;
        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.UnitOfMeasure = request.UnitOfMeasure.Trim().ToUpperInvariant();
        product.UnitPrice = request.UnitPrice;
        product.MinStockQuantity = request.MinStockQuantity;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductResponse>.Ok(ProductResponse.From(product));
    }

    public async Task<ServiceResult<ProductResponse>> AdjustStockAsync(Guid id, AdjustStockRequest request, CancellationToken cancellationToken)
    {
        if (request.QuantityDelta == 0)
            return ServiceResult<ProductResponse>.Fail("A quantidade de ajuste precisa ser diferente de zero.");

        var product = await db.Products.FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
        if (product is null)
            return ServiceResult<ProductResponse>.Fail("Produto nao encontrado.", StatusCodes.Status404NotFound);

        var newStock = product.StockQuantity + request.QuantityDelta;
        if (newStock < 0)
            return ServiceResult<ProductResponse>.Fail("O ajuste deixaria o estoque negativo.");

        product.StockQuantity = newStock;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            QuantityDelta = request.QuantityDelta,
            Type = InventoryMovementType.ManualAdjustment,
            Notes = string.IsNullOrWhiteSpace(request.Reason) ? "Ajuste manual de estoque" : request.Reason.Trim()
        });

        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductResponse>.Ok(ProductResponse.From(product));
    }

    private static string Normalize(string value) => value.Trim();

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? ValidateProduct(
        string barcode,
        string name,
        string unitOfMeasure,
        decimal unitPrice,
        decimal stockQuantity,
        decimal minStockQuantity)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return "Codigo de barras e obrigatorio.";

        if (string.IsNullOrWhiteSpace(name))
            return "Nome do produto e obrigatorio.";

        if (string.IsNullOrWhiteSpace(unitOfMeasure))
            return "Unidade de medida e obrigatoria.";

        if (unitPrice < 0)
            return "Preco unitario nao pode ser negativo.";

        if (stockQuantity < 0)
            return "Estoque nao pode ser negativo.";

        if (minStockQuantity < 0)
            return "Estoque minimo nao pode ser negativo.";

        return null;
    }
}
