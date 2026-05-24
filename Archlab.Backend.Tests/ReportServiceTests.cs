using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Archlab.Backend.Contracts;
using Archlab.Backend.Domain;
using Archlab.Backend.Services;
using Archlab.Backend.Tests.Helpers;

namespace Archlab.Backend.Tests;

public sealed class ReportServiceTests : IDisposable
{
    private readonly Archlab.Backend.Data.PdvDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly ReportService _sut;
    private readonly SaleService _saleService;
    private static readonly Guid SessionId = Guid.NewGuid();

    public ReportServiceTests()
    {
        (_db, _connection) = DbContextFactory.CreateWithConnection();
        _sut = new ReportService(_db);
        _saleService = new SaleService(_db, new FiscalDocumentService(_db), NullLogger<SaleService>.Instance);
        SeedAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SeedAsync()
    {
        // SaleCounter and FiscalCounter are already seeded by DbContextFactory.CreateWithConnection()
        _db.Products.Add(new Product
        {
            Barcode = "7891000100103",
            Name = "Arroz 5kg",
            UnitOfMeasure = "UN",
            UnitPrice = 24.90m,
            StockQuantity = 200,
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

    private CreateSaleRequest BuildSale(decimal paymentAmount = 25m) => new(
        CashSessionId: SessionId,
        OperatorName: "Op",
        CustomerDocument: null,
        CustomerId: null,
        SaleDiscountTotal: 0m,
        Items: new List<CreateSaleItemRequest> { new("7891000100103", 1, 0m) },
        Payments: new List<CreatePaymentRequest> { new(PaymentMethod.Cash, paymentAmount, null) },
        IssueFiscalDocument: false);

    [Fact]
    public async Task GetSalesSummary_WithCompletedSales_ReturnsTotals()
    {
        await _saleService.CreateAsync(BuildSale(), default);
        await _saleService.CreateAsync(BuildSale(), default);

        var from = DateTimeOffset.UtcNow.AddHours(-1);
        var to = DateTimeOffset.UtcNow.AddHours(1);

        var result = await _sut.GetSalesSummaryAsync(from, to, null, default);

        Assert.Equal(2, result.TotalSales);
        Assert.Equal(49.80m, result.TotalRevenue);
        Assert.Equal(49.80m, result.NetRevenue);
    }

    [Fact]
    public async Task GetSalesSummary_NoSalesInRange_ReturnsZeros()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-10);
        var to = DateTimeOffset.UtcNow.AddDays(-9);

        var result = await _sut.GetSalesSummaryAsync(from, to, null, default);

        Assert.Equal(0, result.TotalSales);
        Assert.Equal(0m, result.NetRevenue);
    }

    [Fact]
    public async Task GetSalesSummary_ByPaymentMethod_IncludesCash()
    {
        await _saleService.CreateAsync(BuildSale(), default);

        var from = DateTimeOffset.UtcNow.AddHours(-1);
        var to = DateTimeOffset.UtcNow.AddHours(1);

        var result = await _sut.GetSalesSummaryAsync(from, to, null, default);

        Assert.Contains(result.ByPaymentMethod, m => m.Method == "Cash");
    }

    [Fact]
    public async Task GetStockAlerts_ProductBelowMin_Returned()
    {
        var product = await _db.Products.FirstAsync(p => p.Barcode == "7891000100103");
        product.StockQuantity = 5;
        product.MinStockQuantity = 10;
        await _db.SaveChangesAsync();

        var alerts = await _sut.GetStockAlertsAsync(default);

        Assert.Contains(alerts, a => a.Barcode == "7891000100103");
        Assert.All(alerts, a => Assert.True(a.StockQuantity <= a.MinStockQuantity));
    }

    [Fact]
    public async Task GetStockAlerts_ProductAboveMin_NotIncluded()
    {
        var product = await _db.Products.FirstAsync(p => p.Barcode == "7891000100103");
        product.StockQuantity = 100;
        product.MinStockQuantity = 10;
        await _db.SaveChangesAsync();

        var alerts = await _sut.GetStockAlertsAsync(default);

        Assert.DoesNotContain(alerts, a => a.Barcode == "7891000100103");
    }

    [Fact]
    public async Task GetCashSessionSummary_ValidSession_ReturnsSummary()
    {
        await _saleService.CreateAsync(BuildSale(), default);

        var result = await _sut.GetCashSessionSummaryAsync(SessionId, default);

        Assert.True(result.Succeeded);
        Assert.Equal("CAIXA-01", result.Value!.Session.TerminalId);
        Assert.Equal(1, result.Value.Sales.TotalSales);
    }

    [Fact]
    public async Task GetCashSessionSummary_UnknownSession_Returns404()
    {
        var result = await _sut.GetCashSessionSummaryAsync(Guid.NewGuid(), default);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.Error!.StatusCode);
    }

    [Fact]
    public async Task GetTopProducts_ReturnsMostSold()
    {
        await _saleService.CreateAsync(BuildSale(), default);
        await _saleService.CreateAsync(BuildSale(), default);

        var from = DateTimeOffset.UtcNow.AddHours(-1);
        var to = DateTimeOffset.UtcNow.AddHours(1);

        var result = await _sut.GetTopProductsAsync(from, to, 10, default);

        Assert.Single(result);
        Assert.Equal("7891000100103", result[0].Barcode);
        Assert.Equal(2, result[0].TotalQuantity);
    }

    [Fact]
    public async Task GetRevenueByDay_ReturnsDailyGrouping()
    {
        await _saleService.CreateAsync(BuildSale(), default);
        await _saleService.CreateAsync(BuildSale(), default);

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddDays(1);

        var result = await _sut.GetRevenueByDayAsync(from, to, default);

        Assert.Single(result);
        Assert.Equal(2, result[0].SaleCount);
        Assert.Equal(49.80m, result[0].NetRevenue);
    }

    [Fact]
    public async Task GetInventoryMovements_AfterSale_HasMovement()
    {
        await _saleService.CreateAsync(BuildSale(), default);

        var productId = (await _db.Products.FirstAsync(p => p.Barcode == "7891000100103")).Id;
        var result = await _sut.GetInventoryMovementsAsync(productId, null, null, 1, 20, default);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(-1, result.Items[0].QuantityDelta);
    }
}
