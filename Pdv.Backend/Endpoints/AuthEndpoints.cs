using Pdv.Backend.Contracts;
using Pdv.Backend.Services;

namespace Pdv.Backend.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Autenticacao")
            .AllowAnonymous();

        group.MapPost("/login", async (
            LoginRequest request,
            AuthService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.LoginAsync(request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : Results.Problem(result.Error!.Message, statusCode: result.Error.StatusCode);
        }).WithName("Login");

        return app;
    }
}
