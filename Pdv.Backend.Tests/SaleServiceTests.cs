using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pdv.Backend.Contracts;
using Pdv.Backend.Domain;
using Pdv.Backend.Services;
using Pdv.Backend.Tests.Helpers;

namespace Pdv.Backend.Tests;

public sealed class SaleServiceTests : IDisposable
{
    private readonly Pdv.Backend.Data.PdvDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly SaleService _sut;
    private static readonly Guid SessionId = Guid.NewGuid();

    public SaleServiceTests()
    {
        (_db, _connection) = DbContextFactory.CreateWithConnection();
        _sut = new SaleService(_db, new FiscalDocumentService(_db), NullLogger<SaleService>.Instance);
        SeedAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SeedAsync()
    {
        _db.SaleCounters.Add(new SaleCounter { Id = 1, LastNumber = 0 });
        _db.Products.Add(new Product
        {
            Barcode = "7891000100103",
            Name = "Arroz 5kg",
            UnitOfMeasure = "UN",
            UnitPrice = 24.90m,
            StockQuantity = 80,
            MinStockQuantity = 10
        });
        _db.CashSessions.Add(new CashSession
        {
            Id = SessionId,
            TerminalId = "CAIXA-01",
            OperatorName = "Teste",
            OpeningAmount = 100m,
            Status = CashSessionStatus.Open
        });
        await _db.SaveChangesAsync();
    }

    private static CreateSaleRequest BuildSaleRequest(decimal paymentAmount = 25m) => new(
        CashSessionId: SessionId,
        OperatorName: "Operador Teste",
        CustomerDocument: null,
        CustomerId: null,
        SaleDiscountTotal: 0m,
        Items: new List<CreateSaleItemRequest> { new("7891000100103", 1, 0m) },
        Payments: new List<CreatePaymentRequest> { new(PaymentMethod.Cash, paymentAmount, null) },
        IssueFiscalDocument: false);

    [Fact]
    public async Task CreateSale_ValidRequest_Succeeds()
    {
        var result = await _sut.CreateAsync(BuildSaleRequest(), default);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.Number);
        Assert.Equal(24.90m, result.Value.NetTotal);
    }

    [Fact]
    public async Task CreateSale_DeductsStock()
    {
        await _sut.CreateAsync(BuildSaleRequest(), default);

        var product = await _db.Products.FirstAsync(p => p.Barcode == "7891000100103");
        Assert.Equal(79, product.StockQuantity);
    }

    [Fact]
    public async Task CreateSale_InsufficientPayment_Fails()
    {
        var result = await _sut.CreateAsync(BuildSaleRequest(paymentAmount: 10m), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateSale_InsufficientStock_Fails()
    {
        var product = await _db.Products.FirstAsync(p => p.Barcode == "7891000100103");
        product.StockQuantity = 0;
        await _db.SaveChangesAsync();

        var result = await _sut.CreateAsync(BuildSaleRequest(), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CancelSale_RestoresStock()
    {
        var sale = await _sut.CreateAsync(BuildSaleRequest(), default);

        var cancelResult = await _sut.CancelAsync(sale.Value!.Id, new CancelSaleRequest("Teste de cancelamento"), default);

        Assert.True(cancelResult.Succeeded);
        var product = await _db.Products.FirstAsync(p => p.Barcode == "7891000100103");
        Assert.Equal(80m, product.StockQuantity);
    }

    [Fact]
    public async Task CancelSale_AlreadyCancelled_Fails()
    {
        var sale = await _sut.CreateAsync(BuildSaleRequest(), default);
        await _sut.CancelAsync(sale.Value!.Id, new CancelSaleRequest("1a vez"), default);

        var result = await _sut.CancelAsync(sale.Value.Id, new CancelSaleRequest("2a vez"), default);

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task ListSales_Pagination_Works()
    {
        for (var i = 0; i < 5; i++)
            await _sut.CreateAsync(BuildSaleRequest(), default);

        var page1 = await _sut.ListAsync(null, null, null, 1, 3, default);
        var page2 = await _sut.ListAsync(null, null, null, 2, 3, default);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
    }
}
