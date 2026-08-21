using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Archlab.Backend.Domain;
using Archlab.Backend.Services;
using Archlab.Backend.Tests.Helpers;

namespace Archlab.Backend.Tests;

/// <summary>
/// Revenue-by-day and sales-by-hour collapse an instant into a calendar day and an hour, which
/// only mean something in the store's timezone. These pin that the zone comes from configuration
/// and never from whatever timezone the host happens to be set to.
/// </summary>
/// <remarks>
/// The sale below is rung up at 23:30 on 9 March in Sao Paulo, which is 02:30 on 10 March in UTC —
/// the case that used to book a Monday-evening sale to Tuesday in the container and to Monday on
/// the developer's machine, from the same row. March is deliberate: Brazil has had no DST since
/// 2019, so the offset is a flat -03:00 and the expected values need no transition reasoning.
/// </remarks>
public sealed class ReportTimeZoneTests : IDisposable
{
    private const string SaoPaulo = "America/Sao_Paulo";

    /// <summary>23:30 on 9 March in Sao Paulo; 02:30 on 10 March in UTC.</summary>
    private static readonly DateTimeOffset SoldAt = new(2026, 3, 10, 2, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset WindowStart = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowEnd = new(2026, 3, 31, 23, 59, 59, TimeSpan.Zero);

    private readonly Archlab.Backend.Data.PdvDbContext _db;
    private readonly SqliteConnection _connection;

    public ReportTimeZoneTests()
    {
        (_db, _connection) = DbContextFactory.CreateWithConnection();
        SeedOneSaleAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData(SaoPaulo, 2026, 3, 9)]
    [InlineData("UTC", 2026, 3, 10)]
    public async Task Receita_por_dia_usa_o_fuso_configurado(string timeZoneId, int year, int month, int day)
    {
        var sut = new ReportService(_db, ConfigFor(timeZoneId));

        var days = await sut.GetRevenueByDayAsync(WindowStart, WindowEnd, default);

        var entry = Assert.Single(days);
        Assert.Equal(new DateOnly(year, month, day), entry.Date);
    }

    [Theory]
    [InlineData(SaoPaulo, 23)]
    [InlineData("UTC", 2)]
    public async Task Vendas_por_hora_usam_o_fuso_configurado(string timeZoneId, int expectedHour)
    {
        var sut = new ReportService(_db, ConfigFor(timeZoneId));

        var summary = await sut.GetSalesSummaryAsync(WindowStart, WindowEnd, null, default);

        var entry = Assert.Single(summary.ByHour);
        Assert.Equal(expectedHour, entry.Hour);
    }

    /// <summary>
    /// An id the host cannot resolve must not silently hand the grouping back to the host's own
    /// timezone — that is the failure this whole file exists to prevent.
    /// </summary>
    [Theory]
    [InlineData("Nao/Existe")]
    [InlineData("")]
    // Cast so the compiler builds a one-element array instead of reading the bare null as the
    // params array itself.
    [InlineData((string?)null)]
    public async Task Fuso_ausente_ou_desconhecido_cai_para_UTC(string? timeZoneId)
    {
        var sut = new ReportService(_db, ConfigFor(timeZoneId));

        var days = await sut.GetRevenueByDayAsync(WindowStart, WindowEnd, default);

        Assert.Equal(new DateOnly(2026, 3, 10), Assert.Single(days).Date);
    }

    private static IConfiguration ConfigFor(string? timeZoneId) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [StoreTimeZone.ConfigurationKey] = timeZoneId })
            .Build();

    private async Task SeedOneSaleAsync()
    {
        // Guards the InlineData above: without tzdata the service falls back to UTC and the
        // Sao_Paulo rows would fail with an off-by-three-hours mystery instead of this sentence.
        Assert.True(
            TimeZoneInfo.TryFindSystemTimeZoneById(SaoPaulo, out _),
            $"O host nao resolve o fuso {SaoPaulo}; instale a base de fusos (tzdata) para rodar estes testes.");

        var session = new CashSession
        {
            TerminalId = "CAIXA-01",
            OperatorName = "Teste",
            OpeningAmount = 0m
        };
        _db.CashSessions.Add(session);
        // Saved on its own so the sale's foreign key points at a row that already exists — the
        // sale sets the key as a scalar, with no navigation for EF to order the inserts by.
        await _db.SaveChangesAsync();

        _db.Sales.Add(new Sale
        {
            Number = 1,
            CashSessionId = session.Id,
            TerminalId = session.TerminalId,
            OperatorName = session.OperatorName,
            CreatedAt = SoldAt,
            GrossTotal = 100m,
            NetTotal = 100m,
            PaidAmount = 100m,
            Status = SaleStatus.Completed
        });

        await _db.SaveChangesAsync();
    }
}
