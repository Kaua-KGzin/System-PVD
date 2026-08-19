using System.Net;

namespace Archlab.Backend.Tests.Integration;

/// <summary>
/// The two flags on ArchlabApi.Build are what separate the container deployment from the desktop
/// shell, and the reasons are documented but were never pinned by a test. These are the guarantees
/// the desktop depends on.
/// </summary>
public class DeploymentShapeTests
{
    /// <summary>
    /// The desktop serves the SPA and the API from one origin, so no cross-origin caller exists
    /// and the missing Cors:AllowedOrigins is correct rather than an oversight.
    /// </summary>
    [Fact]
    public async Task Shape_desktop_sobe_em_producao_sem_origens_de_cors()
    {
        await using var api = await ApiFactory.CreateAsync(
            httpsRedirection: false,
            serveStaticFiles: true,
            environment: "Production");

        var response = await api.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Client-side routes are not server routes. A cashier deep-linking to /pdv must still get the
    /// shell instead of a 404 from a route that only exists inside React Router.
    /// </summary>
    [Fact]
    public async Task Shape_desktop_devolve_o_shell_da_spa_em_rota_do_cliente()
    {
        await using var api = await ApiFactory.CreateAsync(
            serveStaticFiles: true,
            environment: "Production");

        var response = await api.Client.GetAsync("/pdv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ARCHNEXUS", await response.Content.ReadAsStringAsync());
    }

    /// <summary>The SPA fallback must not swallow the API: /api/* still answers as an API.</summary>
    [Fact]
    public async Task Fallback_da_spa_nao_engole_as_rotas_de_api()
    {
        await using var api = await ApiFactory.CreateAsync(
            serveStaticFiles: true,
            environment: "Production");

        var response = await api.Client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// With httpsRedirection on, a plain HTTP request is redirected. The desktop turns this off
    /// because it has no HTTPS endpoint to redirect to.
    /// </summary>
    /// <remarks>
    /// The port has to be handed over explicitly: the middleware silently does nothing when it
    /// cannot determine one, and TestServer binds no real HTTPS endpoint for it to discover. Under
    /// Kestrel the port comes from the server's own addresses.
    /// </remarks>
    [Fact]
    public async Task Redirecionamento_https_ligado_redireciona_requisicao_http()
    {
        await using var api = await ApiFactory.CreateAsync(
            httpsRedirection: true,
            extraSettings: new Dictionary<string, string?> { ["https_port"] = "443" });

        var response = await api.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.RedirectKeepVerb, response.StatusCode);
        Assert.StartsWith("https://", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Redirecionamento_https_desligado_responde_direto()
    {
        await using var api = await ApiFactory.CreateAsync(httpsRedirection: false);

        var response = await api.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
