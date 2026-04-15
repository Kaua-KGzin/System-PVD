using Pdv.Backend.Common;
using Pdv.Backend.Contracts;
using Pdv.Backend.Services;

namespace Pdv.Backend.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products")
            .WithTags("Produtos");

        group.MapGet("/", async (
            ProductService service,
            string? search = null,
            bool includeInactive = false,
            CancellationToken cancellationToken = default) =>
            Results.Ok(await service.SearchAsync(search, includeInactive, cancellationToken)))
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
