using Archlab.Backend.Common;
using Archlab.Backend.Contracts;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

public static class CommissionEndpoints
{
    public static IEndpointRouteBuilder MapCommissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/commissions")
            .WithTags("Comissoes")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/", async (
            CommissionService service,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            return Results.Ok(await service.ListAsync(resolvedPage, resolvedPageSize, ct));
        }).WithName("ListCommissions");

        group.MapPost("/", async (
            CreateSellerCommissionRequest request,
            CommissionService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateAsync(request, ct);
            return result.ToCreatedResult(c => $"/api/commissions/{c.Id}");
        }).WithName("CreateCommission");

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateSellerCommissionRequest request,
            CommissionService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateAsync(id, request, ct);
            return result.ToHttpResult();
        }).WithName("UpdateCommission");

        group.MapGet("/summary/{userId:guid}", async (
            Guid userId,
            DateTimeOffset from,
            DateTimeOffset to,
            CommissionService service,
            CancellationToken ct) =>
        {
            var result = await service.GetSummaryAsync(userId, from, to, ct);
            return Results.Ok(result);
        }).WithName("GetCommissionSummary");

        return app;
    }
}
