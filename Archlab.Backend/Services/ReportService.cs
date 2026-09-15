using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Services;

public sealed class ReportService(PdvDbContext db)
{
    public async Task<SalesSummaryResponse> GetSalesSummaryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? terminalId,
        CancellationToken cancellationToken)
    {
        var baseQuery = db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed
                     && s.CreatedAt >= from
                     && s.CreatedAt <= to);

        if (!string.IsNullOrWhiteSpace(terminalId))
            baseQuery = baseQuery.Where(s => s.TerminalId == terminalId);

        var totals = await GetTotalsAsync(baseQuery, cancellationToken);
        var byMethod = await GetByMethodAsync(from, to, terminalId, cancellationToken);
        var byHour = await GetByHourAsync(baseQuery, cancellationToken);

        var (totalSales, totalRevenue, totalDiscounts, netRevenue) = totals;

        return new SalesSummaryResponse(
            totalSales,
            totalRevenue,
            totalDiscounts,
            netRevenue,
            byMethod,
            byHour);
    }

    private static async Task<(int Count, decimal GrossTotal, decimal Discounts, decimal NetTotal)>
        GetTotalsAsync(IQueryable<Sale> query, CancellationToken ct)
    {
        var result = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                GrossTotal = g.Sum(s => s.GrossTotal),
                Discounts = g.Sum(s => s.ItemDiscountTotal + s.SaleDiscountTotal),
                NetTotal = g.Sum(s => s.NetTotal)
            })
            .FirstOrDefaultAsync(ct);

        return result is null
            ? (0, 0, 0, 0)
            : (result.Count, result.GrossTotal, result.Discounts, result.NetTotal);
    }

    private async Task<SalesByPaymentMethodEntry[]> GetByMethodAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? terminalId,
        CancellationToken ct)
    {
        var paymentsQuery = db.SalePayments.AsNoTracking()
            .Where(p => p.Sale!.Status == SaleStatus.Completed
                     && p.Sale.CreatedAt >= from
                     && p.Sale.CreatedAt <= to);

        if (!string.IsNullOrWhiteSpace(terminalId))
            paymentsQuery = paymentsQuery.Where(p => p.Sale!.TerminalId == terminalId);

        return await paymentsQuery
            .GroupBy(p => p.Method)
            .Select(g => new SalesByPaymentMethodEntry(
                g.Key.ToString(),
                g.Count(),
                g.Sum(p => p.Amount)))
            .ToArrayAsync(ct);
    }

    private static async Task<SalesByHourEntry[]> GetByHourAsync(
        IQueryable<Sale> query, CancellationToken ct)
    {
        // Hour extraction must be client-side because EF provider support varies.
        // Only CreatedAt + NetTotal are fetched — minimal payload.
        var sales = await query
            .Select(s => new { s.CreatedAt, s.NetTotal })
            .ToArrayAsync(ct);

        return [.. sales
            .GroupBy(s => s.CreatedAt.ToLocalTime().Hour)
            .Select(g => new SalesByHourEntry(g.Key, g.Count(), g.Sum(s => s.NetTotal)))
            .OrderBy(e => e.Hour)];
    }

    public async Task<StockAlertEntry[]> GetStockAlertsAsync(CancellationToken cancellationToken)
    {
        return await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.StockQuantity <= p.MinStockQuantity)
            .OrderByDescending(p => p.MinStockQuantity - p.StockQuantity)
            .Select(p => new StockAlertEntry(
                p.Id,
                p.Barcode,
                p.Name,
                p.StockQuantity,
                p.MinStockQuantity,
                p.MinStockQuantity - p.StockQuantity))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ServiceResult<CashSessionSummaryResponse>> GetCashSessionSummaryAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await db.CashSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
            return ServiceResult<CashSessionSummaryResponse>.Fail("Sessao nao encontrada.", StatusCodes.Status404NotFound);

        var salesSummary = await GetSessionSalesSummaryAsync(sessionId, cancellationToken);
        var paymentBreakdown = await GetSessionPaymentBreakdownAsync(sessionId, cancellationToken);

        var expectedClosing = await GetExpectedCashClosingAsync(session.Id, session.OpeningAmount, cancellationToken);

        decimal? difference = session.ClosingAmount.HasValue
            ? session.ClosingAmount.Value - expectedClosing
            : null;

        var response = new CashSessionSummaryResponse(
            new CashSessionInfo(
                session.Id,
                session.TerminalId,
                session.OperatorName,
                session.OpeningAmount,
                session.OpenedAt,
                session.ClosedAt,
                session.Status.ToString()),
            salesSummary,
            paymentBreakdown,
            expectedClosing,
            session.ClosingAmount,
            difference);

        return ServiceResult<CashSessionSummaryResponse>.Ok(response);
    }

    private async Task<decimal> GetExpectedCashClosingAsync(
        Guid sessionId,
        decimal openingAmount,
        CancellationToken ct)
    {
        var cashReceived = await db.SalePayments.AsNoTracking()
            .Where(p => p.Method == PaymentMethod.Cash &&
                        p.Sale!.CashSessionId == sessionId &&
                        p.Sale.Status == SaleStatus.Completed)
            .SumAsync(p => p.Amount, ct);

        var changePaid = await db.Sales.AsNoTracking()
            .Where(s => s.CashSessionId == sessionId && s.Status == SaleStatus.Completed)
            .SumAsync(s => s.ChangeAmount, ct);

        return openingAmount + cashReceived - changePaid;
    }

    private async Task<CashSessionSalesSummary> GetSessionSalesSummaryAsync(
        Guid sessionId, CancellationToken ct)
    {
        var result = await db.Sales.AsNoTracking()
            .Where(s => s.CashSessionId == sessionId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Cancelled = g.Count(s => s.Status == SaleStatus.Cancelled),
                GrossRevenue = g.Where(s => s.Status == SaleStatus.Completed).Sum(s => s.GrossTotal),
                Discounts = g.Where(s => s.Status == SaleStatus.Completed)
                             .Sum(s => s.ItemDiscountTotal + s.SaleDiscountTotal),
                NetRevenue = g.Where(s => s.Status == SaleStatus.Completed).Sum(s => s.NetTotal)
            })
            .FirstOrDefaultAsync(ct);

        return result is null
            ? new CashSessionSalesSummary(0, 0, 0, 0, 0)
            : new CashSessionSalesSummary(
                result.Total - result.Cancelled,
                result.Cancelled,
                result.GrossRevenue,
                result.Discounts,
                result.NetRevenue);
    }

    private async Task<PaymentBreakdown[]> GetSessionPaymentBreakdownAsync(
        Guid sessionId, CancellationToken ct)
    {
        return await db.SalePayments.AsNoTracking()
            .Where(p => p.Sale!.CashSessionId == sessionId && p.Sale.Status == SaleStatus.Completed)
            .GroupBy(p => p.Method)
            .Select(g => new PaymentBreakdown(g.Key.ToString(), g.Count(), g.Sum(p => p.Amount)))
            .ToArrayAsync(ct);
    }

    public async Task<TopProductEntry[]> GetTopProductsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int limit,
        CancellationToken cancellationToken)
    {
        // Server-side: filter by date and status (avoids full table scan).
        // Client-side: grouping uses Distinct() for sale count, which EF cannot translate
        // to COUNT(DISTINCT) reliably across all providers/versions.
        var rows = await db.SaleItems.AsNoTracking()
            .Where(i => i.Sale!.Status == SaleStatus.Completed
                     && i.Sale.CreatedAt >= from
                     && i.Sale.CreatedAt <= to)
            .Select(i => new { i.ProductId, i.Barcode, i.ProductName, i.Quantity, i.NetTotal, i.SaleId })
            .ToArrayAsync(cancellationToken);

        return [.. rows
            .GroupBy(i => new { i.ProductId, i.Barcode, i.ProductName })
            .Select(g => new TopProductEntry(
                g.Key.ProductId,
                g.Key.Barcode,
                g.Key.ProductName,
                g.Sum(i => i.Quantity),
                g.Select(i => i.SaleId).Distinct().Count(),
                g.Sum(i => i.NetTotal)))
            .OrderByDescending(e => e.TotalRevenue)
            .Take(limit)];
    }

    public async Task<RevenueByDayEntry[]> GetRevenueByDayAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        // Grouping by date requires client-side evaluation because DateOnly conversion
        // is not translatable to SQL on all providers — load only the needed columns.
        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed
                     && s.CreatedAt >= from
                     && s.CreatedAt <= to)
            .Select(s => new { s.CreatedAt, s.GrossTotal, s.ItemDiscountTotal, s.SaleDiscountTotal, s.NetTotal })
            .ToArrayAsync(cancellationToken);

        return [.. sales
            .GroupBy(s => DateOnly.FromDateTime(s.CreatedAt.ToLocalTime().DateTime))
            .Select(g => new RevenueByDayEntry(
                g.Key,
                g.Count(),
                g.Sum(s => s.GrossTotal),
                g.Sum(s => s.ItemDiscountTotal + s.SaleDiscountTotal),
                g.Sum(s => s.NetTotal)))
            .OrderBy(e => e.Date)];
    }

    public async Task<PagedResponse<InventoryMovementEntry>> GetInventoryMovementsAsync(
        Guid? productId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.InventoryMovements.AsNoTracking()
            .Include(m => m.Product)
            .AsQueryable();

        if (productId.HasValue)
            query = query.Where(m => m.ProductId == productId.Value);

        if (from.HasValue)
            query = query.Where(m => m.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(m => m.CreatedAt <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new InventoryMovementEntry(
                m.Id,
                m.ProductId,
                m.Product!.Name,
                m.Product.Barcode,
                m.QuantityDelta,
                m.Type.ToString(),
                m.Notes,
                m.CreatedAt))
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<InventoryMovementEntry>(
            items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}
