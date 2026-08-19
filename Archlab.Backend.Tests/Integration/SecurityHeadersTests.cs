namespace Archlab.Backend.Tests.Integration;

/// <summary>
/// The hardening headers are a hand-written middleware in ArchlabApi.Build. Nothing but a request
/// through the real pipeline can show that they are still being emitted, and on every response
/// rather than only the happy path.
/// </summary>
public class SecurityHeadersTests
{
    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
    public async Task Resposta_carrega_os_cabecalhos_de_seguranca(string header, string expected)
    {
        await using var api = await ApiFactory.CreateAsync();

        var response = await api.Client.GetAsync("/health");

        Assert.True(response.Headers.TryGetValues(header, out var values), $"{header} ausente");
        Assert.Equal(expected, Assert.Single(values!));
    }

    [Fact]
    public async Task Csp_restringe_scripts_a_origem_propria()
    {
        await using var api = await ApiFactory.CreateAsync();

        var response = await api.Client.GetAsync("/health");

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("script-src 'self'", csp);
    }

    /// <summary>A rejected request is still a response, and still needs the headers.</summary>
    [Fact]
    public async Task Resposta_401_tambem_carrega_os_cabecalhos()
    {
        await using var api = await ApiFactory.CreateAsync();

        var response = await api.Client.GetAsync("/api/categories");

        Assert.True(response.Headers.Contains("X-Frame-Options"));
    }
}
