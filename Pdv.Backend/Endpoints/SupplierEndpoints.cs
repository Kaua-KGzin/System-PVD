using Pdv.Backend.Common;
using Pdv.Backend.Contracts;
using Pdv.Backend.Services;

namespace Pdv.Backend.Endpoints;

public static class SupplierEndpoints
{
    public static void MapSupplierEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/suppliers").RequireAuthorization();

        group.MapGet("/", async (
            string? search, bool? isActive,
            int page, int pageSize,
            SupplierService service, CancellationToken ct) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            var result = await service.ListAsync(search, isActive, resolvedPage, resolvedPageSize, ct);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (
            Guid id, SupplierService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.ToHttpResult();
        });

        group.MapPost("/", async (
            CreateSupplierRequest request, SupplierService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.ToCreatedResult(s => $"/api/suppliers/{s.Id}");
        });

        group.MapPut("/{id:guid}", async (
            Guid id, UpdateSupplierRequest request, SupplierService service, CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            return result.ToHttpResult();
        });
    }
}
