namespace Pdv.Backend.Contracts;

public sealed record DashboardResponse(
    TodaySalesStats TodaySales,
    int OpenCashSessions,
    int LowStockProducts,
    RecentSaleEntry[] RecentSales
);

public sealed record TodaySalesStats(
    int Count,
    decimal Total,
    decimal AverageTicket
);

public sealed record RecentSaleEntry(
    Guid Id,
    int Number,
    string TerminalId,
    string OperatorName,
    decimal NetTotal,
    string Status,
    DateTimeOffset CreatedAt
);
