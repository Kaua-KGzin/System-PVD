using Microsoft.EntityFrameworkCore;
using Pdv.Backend.Contracts;
using Pdv.Backend.Data;
using Pdv.Backend.Domain;

namespace Pdv.Backend.Services;

public sealed class CategoryService(PdvDbContext db)
{
    public async Task<CategoryResponse[]> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var query = db.Categories.AsNoTracking();
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.Name)
            .Select(c => CategoryResponse.From(c))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ServiceResult<CategoryResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await db.Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return category is null
            ? ServiceResult<CategoryResponse>.Fail("Categoria nao encontrada.", StatusCodes.Status404NotFound)
            : ServiceResult<CategoryResponse>.Ok(CategoryResponse.From(category));
    }

    public async Task<ServiceResult<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<CategoryResponse>.Fail("Nome da categoria e obrigatorio.");

        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name, cancellationToken))
            return ServiceResult<CategoryResponse>.Fail("Ja existe uma categoria com este nome.", StatusCodes.Status409Conflict);

        var category = new Category
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<CategoryResponse>.Ok(CategoryResponse.From(category));
    }

    public async Task<ServiceResult<CategoryResponse>> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<CategoryResponse>.Fail("Nome da categoria e obrigatorio.");

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
            return ServiceResult<CategoryResponse>.Fail("Categoria nao encontrada.", StatusCodes.Status404NotFound);

        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Id != id && c.Name == name, cancellationToken))
            return ServiceResult<CategoryResponse>.Fail("Ja existe outra categoria com este nome.", StatusCodes.Status409Conflict);

        category.Name = name;
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<CategoryResponse>.Ok(CategoryResponse.From(category));
    }
}
