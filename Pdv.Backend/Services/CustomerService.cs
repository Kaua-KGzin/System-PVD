using Microsoft.EntityFrameworkCore;
using Pdv.Backend.Contracts;
using Pdv.Backend.Data;
using Pdv.Backend.Domain;

namespace Pdv.Backend.Services;

public sealed class CustomerService(PdvDbContext db)
{
    public async Task<PagedResponse<CustomerResponse>> ListAsync(
        string? search,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Customers.AsNoTracking();

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.Name.Contains(term) ||
                (c.Document != null && c.Document.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => CustomerResponse.From(c))
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<CustomerResponse>(items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ServiceResult<CustomerResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return customer is null
            ? ServiceResult<CustomerResponse>.Fail("Cliente nao encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<CustomerResponse>.Ok(CustomerResponse.From(customer));
    }

    public async Task<ServiceResult<CustomerResponse>> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<CustomerResponse>.Fail("Nome do cliente e obrigatorio.");

        var document = NormalizeDoc(request.Document);

        if (document is not null && await db.Customers.AnyAsync(c => c.Document == document, cancellationToken))
            return ServiceResult<CustomerResponse>.Fail("Ja existe um cliente com este documento.", StatusCodes.Status409Conflict);

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Document = document,
            Phone = NullIfEmpty(request.Phone),
            Email = NullIfEmpty(request.Email)
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<CustomerResponse>.Ok(CustomerResponse.From(customer));
    }

    public async Task<ServiceResult<CustomerResponse>> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<CustomerResponse>.Fail("Nome do cliente e obrigatorio.");

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
            return ServiceResult<CustomerResponse>.Fail("Cliente nao encontrado.", StatusCodes.Status404NotFound);

        var document = NormalizeDoc(request.Document);

        if (document is not null && await db.Customers.AnyAsync(c => c.Id != id && c.Document == document, cancellationToken))
            return ServiceResult<CustomerResponse>.Fail("Ja existe outro cliente com este documento.", StatusCodes.Status409Conflict);

        customer.Name = request.Name.Trim();
        customer.Document = document;
        customer.Phone = NullIfEmpty(request.Phone);
        customer.Email = NullIfEmpty(request.Email);
        customer.IsActive = request.IsActive;
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<CustomerResponse>.Ok(CustomerResponse.From(customer));
    }

    private static string? NormalizeDoc(string? doc) =>
        string.IsNullOrWhiteSpace(doc) ? null : new string(doc.Where(char.IsDigit).ToArray());

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
