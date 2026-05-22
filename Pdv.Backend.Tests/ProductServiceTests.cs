using Microsoft.Data.Sqlite;
using Pdv.Backend.Contracts;
using Pdv.Backend.Domain;
using Pdv.Backend.Services;
using Pdv.Backend.Tests.Helpers;

namespace Pdv.Backend.Tests;

public sealed class ProductServiceTests : IDisposable
{
    private readonly Pdv.Backend.Data.PdvDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        (_db, _connection) = DbContextFactory.CreateWithConnection();
        _sut = new ProductService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateProduct_ValidRequest_Succeeds()
    {
        var result = await _sut.CreateAsync(new CreateProductRequest("1234567890123", null, "Produto Teste", "UN", 10.00m, 100, 10), default);

        Assert.True(result.Succeeded);
        Assert.Equal("1234567890123", result.Value!.Barcode);
        Assert.Equal("Produto Teste", result.Value.Name);
    }

    [Fact]
    public async Task CreateProduct_DuplicateBarcode_Fails()
    {
        await _sut.CreateAsync(new CreateProductRequest("1234567890123", null, "Produto A", "UN", 10.00m, 100, 10), default);

        var result = await _sut.CreateAsync(new CreateProductRequest("1234567890123", null, "Produto B", "UN", 20.00m, 50, 5), default);

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task SearchProducts_FilterBySearch_ReturnsMatch()
    {
        await _sut.CreateAsync(new CreateProductRequest("111", null, "Arroz Premium", "KG", 5.00m, 50, 5), default);
        await _sut.CreateAsync(new CreateProductRequest("222", null, "Feijao Preto", "KG", 7.00m, 30, 5), default);

        var result = await _sut.SearchAsync("Arroz", false, null, 1, 20, default);

        Assert.Single(result.Items);
        Assert.Equal("Arroz Premium", result.Items[0].Name);
    }

    [Fact]
    public async Task SearchProducts_FilterByCategory_ReturnsOnlyInCategory()
    {
        var category = new Category { Name = "Graos" };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        await _sut.CreateAsync(new CreateProductRequest("111", null, "Arroz", "KG", 5.00m, 50, 5, category.Id), default);
        await _sut.CreateAsync(new CreateProductRequest("222", null, "Feijao", "KG", 7.00m, 30, 5), default);

        var result = await _sut.SearchAsync(null, false, category.Id, 1, 20, default);

        Assert.Single(result.Items);
        Assert.Equal("Arroz", result.Items[0].Name);
    }

    [Fact]
    public async Task AdjustStock_ValidDelta_UpdatesStock()
    {
        await _sut.CreateAsync(new CreateProductRequest("555", null, "Produto X", "UN", 10m, 100, 5), default);
        var byBarcode = await _sut.GetByBarcodeAsync("555", default);

        var result = await _sut.AdjustStockAsync(byBarcode.Value!.Id, new AdjustStockRequest(-10, "Venda manual"), default);

        Assert.True(result.Succeeded);
        Assert.Equal(90, result.Value!.StockQuantity);
    }

    [Fact]
    public async Task AdjustStock_WouldGoNegative_Fails()
    {
        await _sut.CreateAsync(new CreateProductRequest("666", null, "Produto Y", "UN", 10m, 5, 2), default);
        var byBarcode = await _sut.GetByBarcodeAsync("666", default);

        var result = await _sut.AdjustStockAsync(byBarcode.Value!.Id, new AdjustStockRequest(-100, "Erro"), default);

        Assert.False(result.Succeeded);
    }
}
