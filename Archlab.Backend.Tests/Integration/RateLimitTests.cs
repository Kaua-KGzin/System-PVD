using System.Net;

namespace Archlab.Backend.Tests.Integration;

/// <summary>
/// The global limiter allows 100 requests per minute per partition, with no queue.
/// </summary>
/// <remarks>
/// Each test builds its own host on purpose. The partition key for an unauthenticated caller is
/// the remote IP, which TestServer leaves null — so every test in a shared host would land in the
/// same "anonymous" partition and spend each other's budget.
/// </remarks>
public class RateLimitTests
{
    private const int PermitLimit = 100;

    [Fact]
    public async Task Requisicao_alem_do_limite_devolve_429()
    {
        await using var api = await ApiFactory.CreateAsync();

        for (var i = 0; i < PermitLimit; i++)
        {
            var allowed = await api.Client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        var rejected = await api.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public async Task Dentro_do_limite_nenhuma_requisicao_e_rejeitada()
    {
        await using var api = await ApiFactory.CreateAsync();

        for (var i = 0; i < PermitLimit; i++)
        {
            var response = await api.Client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
