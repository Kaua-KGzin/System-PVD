using Archlab.Backend.Common;
using Archlab.Backend.Contracts;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Clientes")
            .RequireAuthorization();

        group.MapGet("/", async (
            CustomerService service,
            string? search = null,
            bool includeInactive = false,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            return Results.Ok(await service.ListAsync(search, includeInactive, resolvedPage, resolvedPageSize, cancellationToken));
        }).WithName("ListCustomers");

        group.MapGet("/{id:guid}", async (
            Guid id,
            CustomerService service,
            CancellationToken cancellationToken) =>
            (await service.GetByIdAsync(id, cancellationToken)).ToHttpResult())
            .WithName("GetCustomerById");

        group.MapPost("/", async (
            CreateCustomerRequest request,
            CustomerService service,
            CancellationToken cancellationToken) =>
            (await service.CreateAsync(request, cancellationToken))
                .ToCreatedResult(c => $"/api/customers/{c.Id}"))
            .WithName("CreateCustomer");

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCustomerRequest request,
            CustomerService service,
            CancellationToken cancellationToken) =>
            (await service.UpdateAsync(id, request, cancellationToken)).ToHttpResult())
            .WithName("UpdateCustomer");

        return app;
    }
}
