using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Archlab.Backend.Tests.Integration;

/// <summary>
/// Authentication as the pipeline actually applies it. The service tests call AuthService
/// directly, so the JWT bearer handler, its validation parameters, and every RequireAuthorization
/// on the endpoints were previously never executed by a test.
/// </summary>
public class AuthFlowTests
{
    [Fact]
    public async Task Login_valido_devolve_token_e_refresh_token()
    {
        await using var api = await ApiFactory.CreateAsync();

        var response = await api.Client.PostAsJsonAsync("/api/auth/login", new
        {
            username = ApiFactory.AdminUsername,
            password = ApiFactory.AdminPassword
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal("Admin", body.Role);
    }

    [Fact]
    public async Task Login_com_senha_errada_devolve_401()
    {
        await using var api = await ApiFactory.CreateAsync();

        var response = await api.Client.PostAsJsonAsync("/api/auth/login", new
        {
            username = ApiFactory.AdminUsername,
            password = "senha-errada"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Endpoint_protegido_sem_token_devolve_401()
    {
        await using var api = await ApiFactory.CreateAsync();

        var response = await api.Client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Endpoint_protegido_com_token_devolve_200()
    {
        await using var api = await ApiFactory.CreateAsync();
        var token = await api.AdminTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Token_malformado_devolve_401()
    {
        await using var api = await ApiFactory.CreateAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "isto-nao-e-um-jwt");

        var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The signing key is what stops a caller from minting their own admin. A token that is
    /// well-formed and claims the right issuer must still be refused when the signature is not ours.
    /// </summary>
    [Fact]
    public async Task Token_assinado_com_outra_chave_devolve_401()
    {
        await using var api = await ApiFactory.CreateAsync();

        var forged = ForgeToken("outra-chave-completamente-diferente-32b");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_expirado_devolve_401()
    {
        await using var api = await ApiFactory.CreateAsync();

        var expired = ForgeToken(ApiFactory.JwtSecretKey, expiresIn: TimeSpan.FromMinutes(-10));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/categories");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expired);

        var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_troca_o_refresh_token_por_um_par_novo()
    {
        await using var api = await ApiFactory.CreateAsync();

        var login = await api.Client.PostAsJsonAsync("/api/auth/login", new
        {
            username = ApiFactory.AdminUsername,
            password = ApiFactory.AdminPassword
        });
        var issued = await login.Content.ReadFromJsonAsync<LoginBody>();

        var response = await api.Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = issued!.RefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refreshed = await response.Content.ReadFromJsonAsync<LoginBody>();
        Assert.False(string.IsNullOrWhiteSpace(refreshed!.Token));
        Assert.NotEqual(issued.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task Refresh_com_token_invalido_devolve_401()
    {
        await using var api = await ApiFactory.CreateAsync();

        var response = await api.Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = "refresh-token-que-nunca-existiu"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string ForgeToken(string secretKey, TimeSpan? expiresIn = null)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: ApiFactory.JwtIssuer,
            audience: ApiFactory.JwtAudience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, "invasor"),
                new Claim(ClaimTypes.Role, "Admin")
            ],
            expires: DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromHours(1)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record LoginBody(string Token, string RefreshToken, string Username, string Role);
}
