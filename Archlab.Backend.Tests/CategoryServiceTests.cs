using Microsoft.Data.Sqlite;
using Archlab.Backend.Contracts;
using Archlab.Backend.Services;
using Archlab.Backend.Tests.Helpers;

namespace Archlab.Backend.Tests;

public sealed class CategoryServiceTests : IDisposable
{
    private readonly Archlab.Backend.Data.PdvDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly CategoryService _sut;

    public CategoryServiceTests()
    {
        (_db, _connection) = DbContextFactory.CreateWithConnection();
        _sut = new CategoryService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Create_ValidRequest_Succeeds()
    {
        var result = await _sut.CreateAsync(new CreateCategoryRequest("Graos", "Cereais e leguminosas"), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Graos", result.Value!.Name);
        Assert.Equal("Cereais e leguminosas", result.Value.Description);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task Create_DuplicateName_Fails()
    {
        await _sut.CreateAsync(new CreateCategoryRequest("Bebidas", null), default);

        var result = await _sut.CreateAsync(new CreateCategoryRequest("Bebidas", "Outra descricao"), default);

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Create_EmptyName_Fails()
    {
        var result = await _sut.CreateAsync(new CreateCategoryRequest("  ", null), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task List_ReturnsOnlyActive_WhenNotIncludeInactive()
    {
        await _sut.CreateAsync(new CreateCategoryRequest("Ativa", null), default);
        var created = await _sut.CreateAsync(new CreateCategoryRequest("Inativa", null), default);
        await _sut.UpdateAsync(created.Value!.Id, new UpdateCategoryRequest("Inativa", null, false), default);

        var result = await _sut.ListAsync(includeInactive: false, default);

        Assert.Single(result);
        Assert.Equal("Ativa", result[0].Name);
    }

    [Fact]
    public async Task List_IncludeInactive_ReturnsAll()
    {
        await _sut.CreateAsync(new CreateCategoryRequest("Ativa", null), default);
        var created = await _sut.CreateAsync(new CreateCategoryRequest("Inativa", null), default);
        await _sut.UpdateAsync(created.Value!.Id, new UpdateCategoryRequest("Inativa", null, false), default);

        var result = await _sut.ListAsync(includeInactive: true, default);

        Assert.Equal(2, result.Length);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsCategory()
    {
        var created = await _sut.CreateAsync(new CreateCategoryRequest("Limpeza", null), default);

        var result = await _sut.GetByIdAsync(created.Value!.Id, default);

        Assert.True(result.Succeeded);
        Assert.Equal("Limpeza", result.Value!.Name);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), default);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Update_NameConflictWithOther_Fails()
    {
        await _sut.CreateAsync(new CreateCategoryRequest("Frios", null), default);
        var other = await _sut.CreateAsync(new CreateCategoryRequest("Laticinios", null), default);

        var result = await _sut.UpdateAsync(other.Value!.Id, new UpdateCategoryRequest("Frios", null, true), default);

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Update_SameName_Succeeds()
    {
        var created = await _sut.CreateAsync(new CreateCategoryRequest("Padaria", null), default);

        var result = await _sut.UpdateAsync(created.Value!.Id, new UpdateCategoryRequest("Padaria", "Desc atualizada", true), default);

        Assert.True(result.Succeeded);
        Assert.Equal("Desc atualizada", result.Value!.Description);
    }
}
