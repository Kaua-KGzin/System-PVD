using Archlab.Backend.Common;
using Archlab.Backend.Contracts;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

public static class LoyaltyEndpoints
{
    public static IEndpointRouteBuilder MapLoyaltyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/loyalty")
            .WithTags("Fidelidade")
            .RequireAuthorization();

        group.MapGet("/customer/{customerId:guid}", async (
            Guid customerId,
            LoyaltyService service,
            CancellationToken ct) =>
        {
            var result = await service.GetByCustomerAsync(customerId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName("GetCustomerLoyalty");

        group.MapGet("/", async (
            LoyaltyService service,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            return Results.Ok(await service.ListAsync(resolvedPage, resolvedPageSize, ct));
        }).WithName("ListLoyalty");

        group.MapPost("/earn", async (
            EarnPointsRequest request,
            LoyaltyService service,
            CancellationToken ct) =>
        {
            var result = await service.EarnPointsAsync(request.CustomerId, request.SaleId, request.Amount, ct);
            return result.ToHttpResult();
        }).WithName("EarnLoyaltyPoints");

        group.MapPost("/customer/{customerId:guid}/redeem", async (
            Guid customerId,
            RedeemPointsRequest request,
            LoyaltyService service,
            CancellationToken ct) =>
        {
            var result = await service.RedeemPointsAsync(customerId, request.Points, request.Notes, ct);
            return result.ToHttpResult();
        }).WithName("RedeemLoyaltyPoints");

        return app;
    }
}
