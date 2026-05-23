using Microsoft.Data.Sqlite;
using Archlab.Backend.Contracts;
using Archlab.Backend.Domain;
using Archlab.Backend.Services;
using Archlab.Backend.Tests.Helpers;

namespace Archlab.Backend.Tests;

public sealed class CashRegisterServiceTests : IDisposable
{
    private readonly Archlab.Backend.Data.PdvDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly CashRegisterService _sut;

    public CashRegisterServiceTests()
    {
        (_db, _connection) = DbContextFactory.CreateWithConnection();
        _sut = new CashRegisterService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Open_ValidRequest_CreatesOpenSession()
    {
        var result = await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-01", "Operador", 200m), default);

        Assert.True(result.Succeeded);
        Assert.Equal("CAIXA-01", result.Value!.TerminalId);
        Assert.Equal(CashSessionStatus.Open, result.Value.Status);
        Assert.Equal(200m, result.Value.OpeningAmount);
    }

    [Fact]
    public async Task Open_DuplicateTerminal_Fails()
    {
        await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-01", "Op1", 100m), default);

        var result = await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-01", "Op2", 50m), default);

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Open_EmptyTerminalId_Fails()
    {
        var result = await _sut.OpenAsync(new OpenCashSessionRequest("  ", "Op", 100m), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Open_NegativeOpeningAmount_Fails()
    {
        var result = await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-02", "Op", -1m), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Open_SameTerminalAfterClose_Succeeds()
    {
        var opened = await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-01", "Op1", 100m), default);
        await _sut.CloseAsync(opened.Value!.Id, new CloseCashSessionRequest(100m, null), default);

        var result = await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-01", "Op2", 50m), default);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Close_ValidSession_ClosesWithDifference()
    {
        var opened = await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-01", "Op", 100m), default);

        var result = await _sut.CloseAsync(opened.Value!.Id, new CloseCashSessionRequest(110m, "Sobrou 10"), default);

        Assert.True(result.Succeeded);
        Assert.Equal(CashSessionStatus.Closed, result.Value!.Status);
        Assert.Equal(110m, result.Value.ClosingAmount);
        Assert.NotNull(result.Value.ClosingDifference);
    }

    [Fact]
    public async Task Close_AlreadyClosed_Fails()
    {
        var opened = await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-01", "Op", 100m), default);
        await _sut.CloseAsync(opened.Value!.Id, new CloseCashSessionRequest(100m, null), default);

        var result = await _sut.CloseAsync(opened.Value.Id, new CloseCashSessionRequest(100m, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.Error!.StatusCode);
    }

    [Fact]
    public async Task Close_UnknownId_Returns404()
    {
        var result = await _sut.CloseAsync(Guid.NewGuid(), new CloseCashSessionRequest(100m, null), default);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.Error!.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingSession_Returns()
    {
        var opened = await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-03", "Op", 50m), default);

        var result = await _sut.GetByIdAsync(opened.Value!.Id, default);

        Assert.True(result.Succeeded);
        Assert.Equal("CAIXA-03", result.Value!.TerminalId);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), default);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.Error!.StatusCode);
    }

    [Fact]
    public async Task GetOpenByTerminal_ExistingOpen_Returns()
    {
        await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-05", "Op", 150m), default);

        var result = await _sut.GetOpenByTerminalAsync("caixa-05", default);

        Assert.True(result.Succeeded);
        Assert.Equal("CAIXA-05", result.Value!.TerminalId);
    }

    [Fact]
    public async Task GetOpenByTerminal_NoOpenSession_Returns404()
    {
        var result = await _sut.GetOpenByTerminalAsync("CAIXA-99", default);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.Error!.StatusCode);
    }

    [Fact]
    public async Task List_FilterByTerminal_ReturnsOnlyThat()
    {
        await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-A", "Op", 100m), default);
        await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-B", "Op", 100m), default);

        var result = await _sut.ListAsync("CAIXA-A", false, 1, 20, default);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("CAIXA-A", result.Items[0].TerminalId);
    }

    [Fact]
    public async Task List_OnlyOpen_ExcludesClosed()
    {
        var s1 = await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-A", "Op", 100m), default);
        await _sut.OpenAsync(new OpenCashSessionRequest("CAIXA-B", "Op", 100m), default);
        await _sut.CloseAsync(s1.Value!.Id, new CloseCashSessionRequest(100m, null), default);

        var result = await _sut.ListAsync(null, onlyOpen: true, 1, 20, default);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("CAIXA-B", result.Items[0].TerminalId);
    }
}
