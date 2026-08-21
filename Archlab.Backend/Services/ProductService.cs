using System.Data;
using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Services;

public sealed class ProductService(PdvDbContext db)
{
    public async Task<PagedResponse<ProductResponse>> SearchAsync(
        string? search,
        bool includeInactive,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Product> query = db.Products.AsNoTracking().Include(p => p.Category);

        if (!includeInactive)
            query = query.Where(product => product.IsActive);

        if (categoryId.HasValue)
            query = query.Where(product => product.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Contains translates to LIKE, which SQLite matches case-insensitively for ASCII but
            // PostgreSQL does not. Left alone, searching "arroz" finds "Arroz Tipo 1" in dev and
            // nothing in production. Lowering both sides translates to lower() on both providers,
            // and costs no index: a leading-wildcard LIKE could never use one anyway.
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(product =>
                product.Barcode.ToLower().Contains(term) ||
                product.Name.ToLower().Contains(term) ||
                (product.Sku != null && product.Sku.ToLower().Contains(term)));
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
        var product = await db.Products.AsNoTracking().Include(p => p.Category)
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

        return product is null
            ? ServiceResult<ProductResponse>.Fail("Produto nao encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<ProductResponse>.Ok(ProductResponse.From(product));
    }

    public async Task<ServiceResult<ProductResponse>> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        var normalizedBarcode = Normalize(barcode);
        var product = await db.Products.AsNoTracking().Include(p => p.Category)
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

        if (request.CategoryId.HasValue && !await db.Categories.AnyAsync(c => c.Id == request.CategoryId.Value, cancellationToken))
            return ServiceResult<ProductResponse>.Fail("Categoria nao encontrada.", StatusCodes.Status404NotFound);

        var product = new Product
        {
            Barcode = barcode,
            Sku = sku,
            Name = request.Name.Trim(),
            UnitOfMeasure = request.UnitOfMeasure.Trim().ToUpperInvariant(),
            UnitPrice = request.UnitPrice,
            StockQuantity = request.StockQuantity,
            MinStockQuantity = request.MinStockQuantity,
            CategoryId = request.CategoryId,
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

        if (request.CategoryId.HasValue && !await db.Categories.AnyAsync(c => c.Id == request.CategoryId.Value, cancellationToken))
            return ServiceResult<ProductResponse>.Fail("Categoria nao encontrada.", StatusCodes.Status404NotFound);

        product.Barcode = barcode;
        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.UnitOfMeasure = request.UnitOfMeasure.Trim().ToUpperInvariant();
        product.UnitPrice = request.UnitPrice;
        product.MinStockQuantity = request.MinStockQuantity;
        product.CategoryId = request.CategoryId;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        product.RowVersion++;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Moving the token above is what makes two simultaneous edits a real conflict rather
            // than a silent overwrite — so this is now a reachable path, and it answers the way
            // AdjustStockAsync does instead of falling through to a 500.
            return ServiceResult<ProductResponse>.Fail(
                "Este produto foi alterado por outro usuario. Recarregue e tente novamente.", StatusCodes.Status409Conflict);
        }
        catch (Exception ex) when (DbConflict.IsRetryable(ex))
        {
            return ServiceResult<ProductResponse>.Fail(
                "Este produto foi alterado por outro usuario. Recarregue e tente novamente.", StatusCodes.Status409Conflict);
        }

        return ServiceResult<ProductResponse>.Ok(ProductResponse.From(product));
    }

    public async Task<ServiceResult<ProductResponse>> AdjustStockAsync(Guid id, AdjustStockRequest request, CancellationToken cancellationToken)
    {
        if (request.QuantityDelta == 0)
            return ServiceResult<ProductResponse>.Fail("A quantidade de ajuste precisa ser diferente de zero.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var product = await db.Products.FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
        if (product is null)
            return ServiceResult<ProductResponse>.Fail("Produto nao encontrado.", StatusCodes.Status404NotFound);

        var newStock = product.StockQuantity + request.QuantityDelta;
        if (newStock < 0)
            return ServiceResult<ProductResponse>.Fail("O ajuste deixaria o estoque negativo.");

        product.StockQuantity = newStock;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        // Without this the concurrency token never moves, so a sale that read the product before
        // this adjustment still matches on WHERE RowVersion = @original and writes its own stock
        // figure over the adjustment — a lost update that shows up as inventory that drifts.
        product.RowVersion++;

        db.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            QuantityDelta = request.QuantityDelta,
            Type = InventoryMovementType.ManualAdjustment,
            Notes = string.IsNullOrWhiteSpace(request.Reason) ? "Ajuste manual de estoque" : request.Reason.Trim()
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<ProductResponse>.Fail(
                "Conflito de concorrencia ao ajustar o estoque. Tente novamente.", StatusCodes.Status409Conflict);
        }
        catch (Exception ex) when (DbConflict.IsRetryable(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<ProductResponse>.Fail(
                "Conflito de concorrencia ao ajustar o estoque. Tente novamente.", StatusCodes.Status409Conflict);
        }

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
