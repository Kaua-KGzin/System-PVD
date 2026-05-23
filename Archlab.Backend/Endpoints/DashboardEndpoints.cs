using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/dashboard", async (
            PdvDbContext db,
            CancellationToken ct) =>
        {
            var todayStart = new DateTimeOffset(DateTime.Today);
            var todayEnd = todayStart.AddDays(1);

            var todaySalesTask = GetTodaySalesAsync(db, todayStart, todayEnd, ct);
            var openSessionsTask = GetOpenSessionsCountAsync(db, ct);
            var lowStockTask = GetLowStockCountAsync(db, ct);
            var recentSalesTask = GetRecentSalesAsync(db, ct);
            await Task.WhenAll(todaySalesTask, openSessionsTask, lowStockTask, recentSalesTask);
            var todaySales = todaySalesTask.Result;
            var openSessions = openSessionsTask.Result;
            var lowStockCount = lowStockTask.Result;
            var recentSales = recentSalesTask.Result;

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
