using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Contracts;
using Archlab.Backend.Domain;
using Archlab.Backend.Services;
using Archlab.Backend.Tests.Helpers;

namespace Archlab.Backend.Tests;

public sealed class ProductServiceTests : IDisposable
{
    private readonly Archlab.Backend.Data.PdvDbContext _db;
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

    /// <summary>
    /// Product.RowVersion is the concurrency token every writer of a product is checked against.
    /// A write that changes the stock but leaves the token where it was is invisible to the next
    /// writer: a sale that read the product first still matches on WHERE RowVersion = @original
    /// and overwrites the adjustment with its own figure. Whoever moves the stock moves the token.
    /// </summary>
    [Fact]
    public async Task AjusteDeEstoque_avanca_o_token_de_concorrencia()
    {
        const string barcode = "777";
        await _sut.CreateAsync(new CreateProductRequest(barcode, null, "Produto Z", "UN", 10m, 100, 5), default);
        var product = await _db.Products.SingleAsync(p => p.Barcode == barcode);
        var before = product.RowVersion;

        var result = await _sut.AdjustStockAsync(product.Id, new AdjustStockRequest(25, "Conferencia"), default);
        Assert.True(result.Succeeded);

        var after = await _db.Products.AsNoTracking().SingleAsync(p => p.Barcode == barcode);
        Assert.Equal(125, after.StockQuantity);
        Assert.NotEqual(before, after.RowVersion);
    }

    [Fact]
    public async Task Edicao_de_produto_avanca_o_token_de_concorrencia()
    {
        await _sut.CreateAsync(new CreateProductRequest("888", null, "Nome Antigo", "UN", 10m, 100, 5), default);
        var product = await _db.Products.SingleAsync(p => p.Barcode == "888");
        var before = product.RowVersion;

        var result = await _sut.UpdateAsync(
            product.Id,
            new UpdateProductRequest("888", null, "Nome Novo", "UN", 12m, 5, null, true),
            default);
        Assert.True(result.Succeeded);

        var after = await _db.Products.AsNoTracking().SingleAsync(p => p.Barcode == "888");
        Assert.Equal("Nome Novo", after.Name);
        Assert.NotEqual(before, after.RowVersion);
    }

    /// <summary>
    /// An operator types "arroz", not "Arroz". The search must not care.
    /// </summary>
    /// <remarks>
    /// This runs on SQLite, whose LIKE is already case-insensitive for ASCII — so it passed
    /// before the query started lowering both sides, and passes after. It is here to pin the
    /// behaviour for PostgreSQL, where LIKE is case-sensitive and the un-normalized query
    /// returned nothing. Same shape as the note in SaleConcurrencyTests: the assertion that
    /// matters for production needs the Postgres provider to actually run it.
    /// </remarks>
    [Theory]
    [InlineData("arroz")]
    [InlineData("ARROZ")]
    [InlineData("ArRoZ")]
    public async Task Busca_de_produto_ignora_maiusculas(string term)
    {
        await _sut.CreateAsync(new CreateProductRequest("999", null, "Arroz Tipo 1 5kg", "UN", 24.90m, 50, 5), default);

        var result = await _sut.SearchAsync(term, false, null, 1, 20, default);

        Assert.Single(result.Items);
        Assert.Equal("Arroz Tipo 1 5kg", result.Items[0].Name);
    }
}
