using Archlab.Backend.Domain;

namespace Archlab.Backend.Contracts;

public sealed record CreateCategoryRequest(string Name, string? Description);

public sealed record UpdateCategoryRequest(string Name, string? Description, bool IsActive);

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static CategoryResponse From(Category c) =>
        new(c.Id, c.Name, c.Description, c.IsActive, c.CreatedAt);
}
