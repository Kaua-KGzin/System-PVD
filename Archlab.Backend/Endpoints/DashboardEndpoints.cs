using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;
using Archlab.Backend.Services;

namespace Archlab.Backend.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/dashboard", async (
            PdvDbContext db,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            // DateTime.Today is the *host's* midnight, so "vendas de hoje" — the first number on
            // the first screen — was counted over a UTC day in the container. The store's day is
            // the one the operator means.
            var (todayStart, todayEnd) = StoreTimeZone.Today(StoreTimeZone.Resolve(configuration));

            var todaySales = await GetTodaySalesAsync(db, todayStart, todayEnd, ct);
            var openSessions = await GetOpenSessionsCountAsync(db, ct);
            var lowStockCount = await GetLowStockCountAsync(db, ct);
            var recentSales = await GetRecentSalesAsync(db, ct);

            var response = new DashboardResponse(todaySales, openSessions, lowStockCount, recentSales);
            return Results.Ok(response);
        }).RequireAuthorization();
    }

    private static async Task<TodaySalesStats> GetTodaySalesAsync(
        PdvDbContext db, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var result = await db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed
                     && s.CreatedAt >= from
                     && s.CreatedAt < to)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Total = g.Sum(s => s.NetTotal) })
            .FirstOrDefaultAsync(ct);

        if (result is null || result.Count == 0)
            return new TodaySalesStats(0, 0, 0);

        var avg = Math.Round(result.Total / result.Count, 2, MidpointRounding.AwayFromZero);
        return new TodaySalesStats(result.Count, result.Total, avg);
    }

    private static Task<int> GetOpenSessionsCountAsync(PdvDbContext db, CancellationToken ct) =>
        db.CashSessions.AsNoTracking()
            .CountAsync(s => s.Status == CashSessionStatus.Open, ct);

    private static Task<int> GetLowStockCountAsync(PdvDbContext db, CancellationToken ct) =>
        db.Products.AsNoTracking()
            .CountAsync(p => p.IsActive && p.StockQuantity <= p.MinStockQuantity, ct);

    private static Task<RecentSaleEntry[]> GetRecentSalesAsync(PdvDbContext db, CancellationToken ct) =>
        db.Sales.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .Select(s => new RecentSaleEntry(
                s.Id,
                s.Number,
                s.TerminalId,
                s.OperatorName,
                s.NetTotal,
                s.Status.ToString(),
                s.CreatedAt))
            .ToArrayAsync(ct);
}
