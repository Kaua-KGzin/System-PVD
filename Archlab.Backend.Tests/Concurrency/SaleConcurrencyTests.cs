using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Tests.Integration;

namespace Archlab.Backend.Tests.Concurrency;

/// <summary>
/// Sale numbering and stock decrement are the two places where a race costs money: a duplicated
/// number breaks the fiscal sequence, and an oversold item is stock that does not exist.
/// SaleService guards both with a Serializable transaction and a RowVersion concurrency token.
/// </summary>
/// <remarks>
/// These run against SQLite, which serializes writers itself — so a green run proves the logic
/// holds under interleaving, not that the isolation level holds under real contention. The proof
/// that matters for production is the same test against PostgreSQL; the provider is a parameter so
/// that row can be switched on once a Postgres instance is available to the suite.
/// </remarks>
public class SaleConcurrencyTests
{
    [Theory]
    [InlineData("Sqlite")]
    // [InlineData("PostgreSQL")]  // needs a live Postgres; see the remarks above
    public async Task Vendas_concorrentes_nunca_repetem_o_numero(string provider)
    {
        await using var api = await CreateAsync(provider);
        var context = await SetUpTillAsync(api, stock: 200);

        const int attempts = 25;
        var responses = await Task.WhenAll(
            Enumerable.Range(0, attempts).Select(_ => SellOneUnitAsync(api, context)));

        var numbers = new List<int>();
        foreach (var response in responses)
        {
            if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.OK)
            {
                continue;
            }

            var sale = await response.Content.ReadFromJsonAsync<SaleBody>();
            numbers.Add(sale!.Number);
        }

        // Stock covers every attempt, so anything less than all of them means the race itself is
        // failing requests — which a "no duplicates" assertion alone would happily call green.
        Assert.Equal(attempts, numbers.Count);
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [Theory]
    [InlineData("Sqlite")]
    // [InlineData("PostgreSQL")]
    public async Task Disputa_pela_ultima_unidade_nao_deixa_o_estoque_negativo(string provider)
    {
        await using var api = await CreateAsync(provider);
        var context = await SetUpTillAsync(api, stock: 1);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => SellOneUnitAsync(api, context)));

        var succeeded = responses.Count(r =>
            r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK);

        // Exactly one, not "at most one": one unit and five buyers must produce one sale. Allowing
        // zero would let a completely broken endpoint pass this test.
        Assert.Equal(1, succeeded);

        await api.WithDbAsync(async db =>
        {
            var remaining = await db.Products
                .Where(p => p.Barcode == context.Barcode)
                .Select(p => p.StockQuantity)
                .SingleAsync();

            Assert.True(remaining >= 0, $"estoque ficou negativo: {remaining}");
        });
    }

    private static Task<ApiFactory> CreateAsync(string provider) =>
        provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)
            ? ApiFactory.CreateAsync()
            : throw new NotSupportedException(
                $"O provider {provider} exige uma instancia real; configure a connection string antes de ligar essa linha.");

    private static async Task<TillContext> SetUpTillAsync(ApiFactory api, decimal stock)
    {
        var token = await api.AdminTokenAsync();
        var barcode = $"789{Random.Shared.Next(100000, 999999)}";

        var product = await SendAsync(api, token, HttpMethod.Post, "/api/products", new
        {
            barcode,
            sku = (string?)null,
            name = "Produto de teste",
            unitOfMeasure = "UN",
            unitPrice = 10.00m,
            stockQuantity = stock,
            minStockQuantity = 0m
        });
        product.EnsureSuccessStatusCode();

        var session = await SendAsync(api, token, HttpMethod.Post, "/api/cash-sessions", new
        {
            terminalId = "PDV-TESTE",
            operatorName = "Operador de teste",
            openingAmount = 0m
        });
        session.EnsureSuccessStatusCode();

        var opened = await session.Content.ReadFromJsonAsync<CashSessionBody>();
        return new TillContext(token, barcode, opened!.Id);
    }

    private static Task<HttpResponseMessage> SellOneUnitAsync(ApiFactory api, TillContext context) =>
        SendAsync(api, context.Token, HttpMethod.Post, "/api/sales", new
        {
            cashSessionId = context.CashSessionId,
            operatorName = "Operador de teste",
            customerDocument = (string?)null,
            customerId = (Guid?)null,
            saleDiscountTotal = 0m,
            items = new[] { new { barcode = context.Barcode, quantity = 1m, unitDiscount = 0m } },
            payments = new[] { new { method = "Cash", amount = 10.00m } }
        });

    private static Task<HttpResponseMessage> SendAsync(
        ApiFactory api, string token, HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return api.Client.SendAsync(request);
    }

    private sealed record TillContext(string Token, string Barcode, Guid CashSessionId);

    private sealed record SaleBody(Guid Id, int Number);

    private sealed record CashSessionBody(Guid Id);
}
