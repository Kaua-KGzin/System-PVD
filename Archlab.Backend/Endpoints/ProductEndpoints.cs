using Archlab.Backend.Common;
using Archlab.Backend.Contracts;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Produtos")
            .RequireAuthorization();

        group.MapGet("/", async (
            ProductService service,
            string? search = null,
            bool includeInactive = false,
            Guid? categoryId = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            return Results.Ok(await service.SearchAsync(search, includeInactive, categoryId, resolvedPage, resolvedPageSize, cancellationToken));
        })
            .WithName("ListProducts");

        group.MapGet("/{id:guid}", async (
            Guid id,
            ProductService service,
            CancellationToken cancellationToken) =>
            (await service.GetByIdAsync(id, cancellationToken)).ToHttpResult())
            .WithName("GetProductById");

        group.MapGet("/barcode/{barcode}", async (
            string barcode,
            ProductService service,
            CancellationToken cancellationToken) =>
            (await service.GetByBarcodeAsync(barcode, cancellationToken)).ToHttpResult())
            .WithName("GetProductByBarcode");

        group.MapPost("/", async (
            CreateProductRequest request,
            ProductService service,
            CancellationToken cancellationToken) =>
            (await service.CreateAsync(request, cancellationToken))
                .ToCreatedResult(product => $"/api/products/{product.Id}"))
            .WithName("CreateProduct");

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateProductRequest request,
            ProductService service,
            CancellationToken cancellationToken) =>
            (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithName("UpdateProduct");

        group.MapPost("/{id:guid}/stock-adjustments", async (
            Guid id,
            AdjustStockRequest request,
            ProductService service,
            CancellationToken cancellationToken) =>
            (await service.AdjustStockAsync(id, request, cancellationToken)).ToHttpResult())
            .WithName("AdjustProductStock");

        return app;
    }
}
