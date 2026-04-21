using Pdv.Backend.Common;
using Pdv.Backend.Services;

namespace Pdv.Backend.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reports").RequireAuthorization();

        group.MapGet("/sales-summary", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? terminalId,
            ReportService reportService,
            CancellationToken ct) =>
        {
            var resolvedFrom = from ?? DateTimeOffset.UtcNow.Date;
            var resolvedTo = to ?? DateTimeOffset.UtcNow;
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

        group.MapGet("/inventory-movements", async (
            Guid? productId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int page,
            int pageSize,
            ReportService reportService,
            CancellationToken ct) =>
        {
            var resolvedPage = page < 1 ? 1 : page;
            var resolvedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            var result = await reportService.GetInventoryMovementsAsync(
                productId, from, to, resolvedPage, resolvedPageSize, ct);
            return Results.Ok(result);
        });
    }
}
