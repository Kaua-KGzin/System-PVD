using Archlab.Backend.Common;
using Archlab.Backend.Contracts;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

public static class SaleEndpoints
{
    public static IEndpointRouteBuilder MapSalesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales")
            .WithTags("Vendas")
            .RequireAuthorization();

        group.MapGet("/", async (
            SaleService service,
            Guid? cashSessionId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            return Results.Ok(await service.ListAsync(cashSessionId, from, to, resolvedPage, resolvedPageSize, cancellationToken));
        })
            .WithName("ListSales");

        group.MapGet("/{id:guid}", async (
            Guid id,
            SaleService service,
            CancellationToken cancellationToken) =>
            (await service.GetByIdAsync(id, cancellationToken)).ToHttpResult())
            .WithName("GetSaleById");

        group.MapPost("/", async (
            CreateSaleRequest request,
            SaleService service,
            CancellationToken cancellationToken) =>
            (await service.CreateAsync(request, cancellationToken))
                .ToCreatedResult(sale => $"/api/sales/{sale.Id}"))
            .WithName("CreateSale")
            .WithValidation<CreateSaleRequest>();

        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelSaleRequest request,
            SaleService service,
            CancellationToken cancellationToken) =>
            (await service.CancelAsync(id, request, cancellationToken)).ToHttpResult())
            .WithName("CancelSale")
            .RequireAuthorization("AdminOrManager")
            .WithValidation<CancelSaleRequest>();

        group.MapPost("/{id:guid}/returns", async (
            Guid id,
            CreateSaleReturnRequest request,
            SaleService service,
            CancellationToken cancellationToken) =>
            (await service.RegisterReturnAsync(id, request, cancellationToken)).ToHttpResult())
            .WithName("RegisterSaleReturn")
            .RequireAuthorization("AdminOrManager");

        group.MapGet("/{id:guid}/returns", async (
            Guid id,
            SaleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListReturnsAsync(id, cancellationToken)))
            .WithName("ListSaleReturns");

        return app;
    }
}
