using Pdv.Backend.Common;
using Pdv.Backend.Contracts;
using Pdv.Backend.Services;

namespace Pdv.Backend.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categorias")
            .RequireAuthorization();

        group.MapGet("/", async (
            CategoryService service,
            bool includeInactive = false,
            CancellationToken cancellationToken = default) =>
            Results.Ok(await service.ListAsync(includeInactive, cancellationToken)))
            .WithName("ListCategories");

        group.MapGet("/{id:guid}", async (
            Guid id,
            CategoryService service,
            CancellationToken cancellationToken) =>
            (await service.GetByIdAsync(id, cancellationToken)).ToHttpResult())
            .WithName("GetCategoryById");

        group.MapPost("/", async (
            CreateCategoryRequest request,
            CategoryService service,
            CancellationToken cancellationToken) =>
            (await service.CreateAsync(request, cancellationToken))
                .ToCreatedResult(c => $"/api/categories/{c.Id}"))
            .WithName("CreateCategory")
            .RequireAuthorization("AdminOnly");

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCategoryRequest request,
            CategoryService service,
            CancellationToken cancellationToken) =>
            (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithName("UpdateCategory")
            .RequireAuthorization("AdminOnly");

        return app;
    }
}
