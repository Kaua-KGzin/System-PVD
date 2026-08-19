using System.Net;

namespace Archlab.Backend.Tests.Integration;

public class SmokeTests
{
    [Fact]
    public async Task Host_sobe_e_responde_o_health_check()
    {
        await using var api = await ApiFactory.CreateAsync();

        var response = await api.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_semeado_consegue_logar()
    {
        await using var api = await ApiFactory.CreateAsync();

        var token = await api.AdminTokenAsync();

        Assert.False(string.IsNullOrWhiteSpace(token));
    }
}
