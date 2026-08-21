using System.Net;
using System.Net.Http.Headers;

namespace Archlab.Backend.Tests.Integration;

/// <summary>
/// Every reporting route must answer with no query string at all.
/// </summary>
/// <remarks>
/// Minimal API binding treats a non-nullable int parameter without a default as a *required*
/// query parameter, so `int page, int pageSize` and `int limit` made three of the routes the
/// README documents answer 400 unless the caller happened to guess that it had to paginate
/// explicitly. Nothing in the SPA calls those three, which is why it went unnoticed — these
/// cover the documented contract rather than the screens.
/// </remarks>
public class ReportEndpointDefaultsTests
{
    public static TheoryData<string> ReportRoutes => new()
    {
        "/api/reports/sales-summary",
        "/api/reports/stock-alerts",
        "/api/reports/inventory-movements",
        "/api/reports/top-products",
        "/api/reports/revenue-by-day",
        "/api/dashboard"
    };

    [Theory]
    [MemberData(nameof(ReportRoutes))]
    public async Task Rota_de_relatorio_responde_sem_query_string(string route)
    {
        await using var api = await ApiFactory.CreateAsync();
        var token = await api.AdminTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await api.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
