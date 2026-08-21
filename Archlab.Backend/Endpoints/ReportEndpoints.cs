using Archlab.Backend.Common;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reports").RequireAuthorization();

        // Every window bound below is converted with ToUniversalTime before it reaches EF, and
        // the calls are not redundant: these bounds are compared against Sale.CreatedAt, which is
        // `timestamp with time zone` on PostgreSQL, and Npgsql throws on a DateTimeOffset
        // parameter whose offset is not zero. A caller passing ?from=2026-03-01T00:00:00-03:00 —
        // the natural thing for a client in the store's own timezone to send — would otherwise
        // get a 500 before the query ran. Same instant, offset zero.

        group.MapGet("/sales-summary", async (
            ReportService reportService,
            IConfiguration configuration,
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? terminalId,
            CancellationToken ct) =>
        {
            var resolvedFrom = (from ?? StoreToday(configuration)).ToUniversalTime();
            var resolvedTo = (to ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var result = await reportService.GetSalesSummaryAsync(resolvedFrom, resolvedTo, terminalId, ct);
            return Results.Ok(result);
        });

        group.MapGet("/stock-alerts", async (
            ReportService reportService,
            CancellationToken ct) =>
        {
            var alerts = await reportService.GetStockAlertsAsync(ct);
            return Results.Ok(alerts);
        });

        group.MapGet("/cash-session-summary/{id:guid}", async (
            Guid id,
            ReportService reportService,
            CancellationToken ct) =>
        {
            var result = await reportService.GetCashSessionSummaryAsync(id, ct);
            return result.ToHttpResult();
        });

        // page/pageSize/limit carry defaults for the same reason the sales list does: a
        // non-nullable int with no default is a *required* query parameter to model binding, so
        // GET /api/reports/inventory-movements — the call the README documents — answered 400
        // until the caller guessed that it had to paginate explicitly.
        group.MapGet("/inventory-movements", async (
            ReportService reportService,
            Guid? productId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            var result = await reportService.GetInventoryMovementsAsync(
                productId, from?.ToUniversalTime(), to?.ToUniversalTime(), resolvedPage, resolvedPageSize, ct);
            return Results.Ok(result);
        });

        group.MapGet("/top-products", async (
            ReportService reportService,
            IConfiguration configuration,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int limit = 10,
            CancellationToken ct = default) =>
        {
            var resolvedFrom = (from ?? StoreToday(configuration)).ToUniversalTime();
            var resolvedTo = (to ?? DateTimeOffset.UtcNow).ToUniversalTime();
            var resolvedLimit = limit is < 1 or > 50 ? 10 : limit;
            return Results.Ok(await reportService.GetTopProductsAsync(resolvedFrom, resolvedTo, resolvedLimit, ct));
        });

        group.MapGet("/revenue-by-day", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            ReportService reportService,
            CancellationToken ct) =>
        {
            var resolvedFrom = (from ?? DateTimeOffset.UtcNow.AddDays(-30)).ToUniversalTime();
            var resolvedTo = (to ?? DateTimeOffset.UtcNow).ToUniversalTime();
            return Results.Ok(await reportService.GetRevenueByDayAsync(resolvedFrom, resolvedTo, ct));
        });
    }

    /// <summary>
    /// Midnight today in the store's timezone, the default lower bound of a report window.
    /// </summary>
    /// <remarks>
    /// DateTimeOffset.UtcNow.Date drops to a DateTime with an unspecified kind, and the implicit
    /// conversion back stamps it with the *host's* offset — so an unfiltered report covered a day
    /// that moved with the machine. It now covers the same day the figures are grouped into.
    /// </remarks>
    private static DateTimeOffset StoreToday(IConfiguration configuration) =>
        StoreTimeZone.Today(StoreTimeZone.Resolve(configuration)).Start;
}
