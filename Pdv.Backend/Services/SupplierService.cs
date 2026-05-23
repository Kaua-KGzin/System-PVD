using Microsoft.EntityFrameworkCore;
using Pdv.Backend.Contracts;
using Pdv.Backend.Data;
using Pdv.Backend.Domain;

namespace Pdv.Backend.Services;

public sealed class SupplierService(PdvDbContext db)
{
    public async Task<PagedResponse<SupplierResponse>> ListAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Suppliers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) || (s.Cnpj != null && s.Cnpj.Contains(search)));

        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => ToResponse(s))
            .ToArrayAsync(ct);

        return new PagedResponse<SupplierResponse>(
            items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ServiceResult<SupplierResponse>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        return supplier is null
            ? ServiceResult<SupplierResponse>.Fail("Fornecedor nao encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<SupplierResponse>.Ok(ToResponse(supplier));
    }

    public async Task<ServiceResult<SupplierResponse>> CreateAsync(CreateSupplierRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<SupplierResponse>.Fail("Nome do fornecedor e obrigatorio.");

        if (!string.IsNullOrWhiteSpace(request.Cnpj))
        {
            var exists = await db.Suppliers.AnyAsync(s => s.Cnpj == request.Cnpj.Trim(), ct);
            if (exists)
                return ServiceResult<SupplierResponse>.Fail("CNPJ ja cadastrado.", StatusCodes.Status409Conflict);
        }

        var supplier = new Supplier
        {
            Name = request.Name.Trim(),
            Cnpj = string.IsNullOrWhiteSpace(request.Cnpj) ? null : request.Cnpj.Trim(),
            ContactName = string.IsNullOrWhiteSpace(request.ContactName) ? null : request.ContactName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim()
        };

        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return ServiceResult<SupplierResponse>.Ok(ToResponse(supplier));
    }

    public async Task<ServiceResult<SupplierResponse>> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null)
            return ServiceResult<SupplierResponse>.Fail("Fornecedor nao encontrado.", StatusCodes.Status404NotFound);

        if (!string.IsNullOrWhiteSpace(request.Cnpj) && request.Cnpj.Trim() != supplier.Cnpj)
        {
            var exists = await db.Suppliers.AnyAsync(s => s.Cnpj == request.Cnpj.Trim() && s.Id != id, ct);
            if (exists)
                return ServiceResult<SupplierResponse>.Fail("CNPJ ja cadastrado.", StatusCodes.Status409Conflict);
        }

        if (!string.IsNullOrWhiteSpace(request.Name)) supplier.Name = request.Name.Trim();
        supplier.Cnpj = string.IsNullOrWhiteSpace(request.Cnpj) ? null : request.Cnpj.Trim();
        supplier.ContactName = string.IsNullOrWhiteSpace(request.ContactName) ? null : request.ContactName.Trim();
        supplier.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        supplier.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (request.IsActive.HasValue) supplier.IsActive = request.IsActive.Value;
        supplier.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return ServiceResult<SupplierResponse>.Ok(ToResponse(supplier));
    }

    private static SupplierResponse ToResponse(Supplier s) =>
        new(s.Id, s.Name, s.Cnpj, s.ContactName, s.Phone, s.Email, s.IsActive, s.CreatedAt);
}
