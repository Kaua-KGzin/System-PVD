using Archlab.Backend.Common;
using Archlab.Backend.Contracts;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Usuarios")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/", async (
            UserService service,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            return Results.Ok(await service.ListAsync(resolvedPage, resolvedPageSize, cancellationToken));
        }).WithName("ListUsers");

        group.MapPost("/", async (
            CreateUserRequest request,
            UserService service,
            CancellationToken cancellationToken) =>
            (await service.CreateAsync(request, cancellationToken))
                .ToCreatedResult(u => $"/api/users/{u.Id}"))
            .WithName("CreateUser");

        group.MapPost("/{id:guid}/deactivate", async (
            Guid id,
            UserService service,
            CancellationToken cancellationToken) =>
            (await service.DeactivateAsync(id, cancellationToken)).ToHttpResult())
            .WithName("DeactivateUser");

        group.MapPost("/{id:guid}/reactivate", async (
            Guid id,
            UserService service,
            CancellationToken cancellationToken) =>
            (await service.ReactivateAsync(id, cancellationToken)).ToHttpResult())
            .WithName("ReactivateUser");

        return app;
    }
}
