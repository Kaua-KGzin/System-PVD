using Pdv.Backend.Common;
using Pdv.Backend.Contracts;
using Pdv.Backend.Services;

namespace Pdv.Backend.Endpoints;

public static class PurchaseEntryEndpoints
{
    public static void MapPurchaseEntryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/purchase-entries").RequireAuthorization();

        group.MapGet("/", async (
            Guid? supplierId, int page, int pageSize,
            PurchaseEntryService service, CancellationToken ct) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            var result = await service.ListAsync(supplierId, resolvedPage, resolvedPageSize, ct);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (
            Guid id, PurchaseEntryService service, CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(id, ct);
            return result.ToHttpResult();
        });

        group.MapPost("/", async (
            CreatePurchaseEntryRequest request, PurchaseEntryService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.ToCreatedResult(e => $"/api/purchase-entries/{e.Id}");
        });
    }
}
