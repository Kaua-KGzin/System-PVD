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
            // ToUniversalTime is load-bearing: these bounds are compared against Sale.CreatedAt,
            // which is `timestamp with time zone` on PostgreSQL, and Npgsql rejects a
            // DateTimeOffset parameter carrying a non-zero offset. Same instant, offset zero.
            return Results.Ok(await service.ListAsync(cashSessionId, from?.ToUniversalTime(), to?.ToUniversalTime(), resolvedPage, resolvedPageSize, cancellationToken));
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


        return app;
    }
}
