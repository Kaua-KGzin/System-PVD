using System.Security.Claims;
using Archlab.Backend.Contracts;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

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
        })
        .WithName("Login")
        .RequireRateLimiting("login"); // 5 tentativas/min por IP — mitiga brute-force

        group.MapPost("/refresh", async (
            RefreshTokenRequest request,
            AuthService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.RefreshAsync(request, cancellationToken);
            return result.Succeeded
                ? Results.Ok(result.Value)
                : Results.Problem(result.Error!.Message, statusCode: result.Error.StatusCode);
        }).WithName("RefreshToken");

        group.MapPost("/logout", async (
            RefreshTokenRequest request,
            AuthService service,
            CancellationToken cancellationToken) =>
        {
            await service.LogoutAsync(request.RefreshToken, cancellationToken);
            return Results.NoContent();
        }).WithName("Logout");

        // Change own password (requires auth)
        app.MapPost("/api/auth/change-password", async (
            ChangePasswordRequest request,
            AuthService authService,
            UserService userService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);
            var result = await userService.ChangePasswordAsync(userId, request, cancellationToken);
            return result.Succeeded
                ? Results.NoContent()
                : Results.Problem(result.Error!.Message, statusCode: result.Error.StatusCode);
        }).RequireAuthorization().WithTags("Autenticacao").WithName("ChangePassword");

        return app;
    }
}
