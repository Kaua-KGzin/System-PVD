using System.Data;
using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Services;

public sealed class PurchaseEntryService(PdvDbContext db)
{
    public async Task<PagedResponse<PurchaseEntryResponse>> ListAsync(
        Guid? supplierId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.PurchaseEntries.AsNoTracking()
            .Include(e => e.Supplier)
            .Include(e => e.Items).ThenInclude(i => i.Product)
            .AsQueryable();

        if (supplierId.HasValue)
            query = query.Where(e => e.SupplierId == supplierId.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(ct);

        return new PagedResponse<PurchaseEntryResponse>(
            items.Select(ToResponse).ToArray(),
            page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ServiceResult<PurchaseEntryResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var entry = await db.PurchaseEntries.AsNoTracking()
            .Include(e => e.Supplier)
            .Include(e => e.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        return entry is null
            ? ServiceResult<PurchaseEntryResponse>.Fail("Entrada nao encontrada.", StatusCodes.Status404NotFound)
            : ServiceResult<PurchaseEntryResponse>.Ok(ToResponse(entry));
    }

    public async Task<ServiceResult<PurchaseEntryResponse>> CreateAsync(
        CreatePurchaseEntryRequest request, CancellationToken ct)
    {
        if (request.Items is null || request.Items.Count == 0)
            return ServiceResult<PurchaseEntryResponse>.Fail("A entrada precisa ter ao menos um item.");

        if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
            return ServiceResult<PurchaseEntryResponse>.Fail("Numero da nota fiscal e obrigatorio.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var invoiceNumber = request.InvoiceNumber.Trim();

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct);
        if (supplier is null)
            return ServiceResult<PurchaseEntryResponse>.Fail("Fornecedor nao encontrado.", StatusCodes.Status404NotFound);

        if (!supplier.IsActive)
            return ServiceResult<PurchaseEntryResponse>.Fail("Fornecedor inativo.");

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToArray();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var entry = new PurchaseEntry
        {
            SupplierId = supplier.Id,
            InvoiceNumber = invoiceNumber,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            ReceivedAt = request.ReceivedAt ?? DateTimeOffset.UtcNow
        };

        foreach (var itemReq in request.Items)
        {
            if (!products.TryGetValue(itemReq.ProductId, out var product))
                return ServiceResult<PurchaseEntryResponse>.Fail($"Produto {itemReq.ProductId} nao encontrado.", StatusCodes.Status404NotFound);

            if (itemReq.Quantity <= 0)
                return ServiceResult<PurchaseEntryResponse>.Fail($"Quantidade invalida para o produto {product.Name}.");

            if (itemReq.UnitCost < 0)
                return ServiceResult<PurchaseEntryResponse>.Fail($"Custo unitario invalido para o produto {product.Name}.");

            var totalCost = Math.Round(itemReq.Quantity * itemReq.UnitCost, 2, MidpointRounding.AwayFromZero);

            entry.Items.Add(new PurchaseEntryItem
            {
                ProductId = product.Id,
                Quantity = itemReq.Quantity,
                UnitCost = itemReq.UnitCost,
                TotalCost = totalCost
            });

            product.StockQuantity += itemReq.Quantity;
            product.UpdatedAt = DateTimeOffset.UtcNow;

            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                QuantityDelta = itemReq.Quantity,
                Type = InventoryMovementType.PurchaseEntry,
                Notes = $"Entrada NF {invoiceNumber}"
            });
        }

        entry.TotalCost = entry.Items.Sum(i => i.TotalCost);

        db.PurchaseEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var created = await db.PurchaseEntries.AsNoTracking()
            .Include(e => e.Supplier)
            .Include(e => e.Items).ThenInclude(i => i.Product)
            .FirstAsync(e => e.Id == entry.Id, ct);

        return ServiceResult<PurchaseEntryResponse>.Ok(ToResponse(created));
    }

    private static PurchaseEntryResponse ToResponse(PurchaseEntry e) =>
        new(e.Id, e.SupplierId, e.Supplier!.Name, e.InvoiceNumber, e.Notes,
            e.TotalCost, e.ReceivedAt, e.CreatedAt,
            e.Items.Select(i => new PurchaseEntryItemResponse(
                i.Id, i.ProductId, i.Product!.Name, i.Product.Barcode,
                i.Quantity, i.UnitCost, i.TotalCost)).ToArray());
}
