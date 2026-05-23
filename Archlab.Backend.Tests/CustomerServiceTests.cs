using Microsoft.Data.Sqlite;
using Archlab.Backend.Contracts;
using Archlab.Backend.Services;
using Archlab.Backend.Tests.Helpers;

namespace Archlab.Backend.Tests;

public sealed class CustomerServiceTests : IDisposable
{
    private readonly Archlab.Backend.Data.PdvDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly CustomerService _sut;

    public CustomerServiceTests()
    {
        (_db, _connection) = DbContextFactory.CreateWithConnection();
        _sut = new CustomerService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Create_ValidRequest_Succeeds()
    {
        var result = await _sut.CreateAsync(
            new CreateCustomerRequest("Maria Silva", "123.456.789-09", "(11) 99999-0001", "maria@email.com"),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal("Maria Silva", result.Value!.Name);
        Assert.Equal("12345678909", result.Value.Document);
        Assert.Equal("(11) 99999-0001", result.Value.Phone);
    }

    [Fact]
    public async Task Create_EmptyName_Fails()
    {
        var result = await _sut.CreateAsync(new CreateCustomerRequest("  ", null, null, null), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_DuplicateDocument_Fails()
    {
        await _sut.CreateAsync(new CreateCustomerRequest("Cliente A", "111.222.333-44", null, null), default);

        var result = await _sut.CreateAsync(new CreateCustomerRequest("Cliente B", "111.222.333-44", null, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Create_DocumentNormalized_MasksStripped()
    {
        var result = await _sut.CreateAsync(
            new CreateCustomerRequest("Joao", "111.222.333-44", null, null), default);

        Assert.Equal("11122233344", result.Value!.Document);
    }

    [Fact]
    public async Task Create_NullDocument_AllowsMultipleCustomers()
    {
        var r1 = await _sut.CreateAsync(new CreateCustomerRequest("Anonimo 1", null, null, null), default);
        var r2 = await _sut.CreateAsync(new CreateCustomerRequest("Anonimo 2", null, null, null), default);

        Assert.True(r1.Succeeded);
        Assert.True(r2.Succeeded);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsCustomer()
    {
        var created = await _sut.CreateAsync(new CreateCustomerRequest("Pedro", null, null, null), default);

        var result = await _sut.GetByIdAsync(created.Value!.Id, default);

        Assert.True(result.Succeeded);
        Assert.Equal("Pedro", result.Value!.Name);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), default);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.Error!.StatusCode);
    }

    [Fact]
    public async Task List_SearchByName_ReturnsMatch()
    {
        await _sut.CreateAsync(new CreateCustomerRequest("Ana Costa", null, null, null), default);
        await _sut.CreateAsync(new CreateCustomerRequest("Bruno Lima", null, null, null), default);

        var result = await _sut.ListAsync("Ana", false, 1, 20, default);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Ana Costa", result.Items[0].Name);
    }

    [Fact]
    public async Task List_SearchByDocument_ReturnsMatch()
    {
        await _sut.CreateAsync(new CreateCustomerRequest("Carlos", "999.888.777-66", null, null), default);
        await _sut.CreateAsync(new CreateCustomerRequest("Daniela", "111.000.222-33", null, null), default);

        var result = await _sut.ListAsync("99988877766", false, 1, 20, default);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Carlos", result.Items[0].Name);
    }

    [Fact]
    public async Task List_Pagination_Works()
    {
        for (var i = 1; i <= 5; i++)
            await _sut.CreateAsync(new CreateCustomerRequest($"Cliente {i:D2}", null, null, null), default);

        var page1 = await _sut.ListAsync(null, false, 1, 3, default);
        var page2 = await _sut.ListAsync(null, false, 2, 3, default);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
    }

    [Fact]
    public async Task Update_DeactivatesCustomer()
    {
        var created = await _sut.CreateAsync(new CreateCustomerRequest("Fulano", null, null, null), default);

        var result = await _sut.UpdateAsync(
            created.Value!.Id,
            new UpdateCustomerRequest("Fulano", null, null, null, false),
            default);

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.IsActive);
    }

    [Fact]
    public async Task Update_DuplicateDocumentWithOther_Fails()
    {
        await _sut.CreateAsync(new CreateCustomerRequest("A", "11111111111", null, null), default);
        var b = await _sut.CreateAsync(new CreateCustomerRequest("B", "22222222222", null, null), default);

        var result = await _sut.UpdateAsync(
            b.Value!.Id,
            new UpdateCustomerRequest("B", "11111111111", null, null, true),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.Error!.StatusCode);
    }
}
