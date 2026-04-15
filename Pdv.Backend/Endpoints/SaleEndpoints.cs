using Pdv.Backend.Common;
using Pdv.Backend.Contracts;
using Pdv.Backend.Services;

namespace Pdv.Backend.Endpoints;

public static class SaleEndpoints
{
    public static IEndpointRouteBuilder MapSalesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales")
            .WithTags("Vendas");

        group.MapGet("/", async (
            SaleService service,
            Guid? cashSessionId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cashSessionId, from, to, cancellationToken)))
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
            .WithName("CreateSale");

        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelSaleRequest request,
            SaleService service,
            CancellationToken cancellationToken) =>
            (await service.CancelAsync(id, request, cancellationToken)).ToHttpResult())
            .WithName("CancelSale");

        return app;
    }
}
