using Pdv.Backend.Domain;

namespace Pdv.Backend.Contracts;

public sealed record SalesSummaryResponse(
    int TotalSales,
    decimal TotalRevenue,
    decimal TotalDiscounts,
    decimal NetRevenue,
    SalesByPaymentMethodEntry[] ByPaymentMethod,
    SalesByHourEntry[] ByHour
);

public sealed record SalesByPaymentMethodEntry(
    string Method,
    int Count,
    decimal Total
);

public sealed record SalesByHourEntry(
    int Hour,
    int Count,
    decimal Total
);

public sealed record StockAlertEntry(
    Guid ProductId,
    string Barcode,
    string Name,
    decimal StockQuantity,
    decimal MinStockQuantity,
    decimal Deficit
);

public sealed record CashSessionSummaryResponse(
    CashSessionInfo Session,
    CashSessionSalesSummary Sales,
    PaymentBreakdown[] ByPaymentMethod,
    decimal ExpectedClosing,
    decimal? ActualClosing,
    decimal? Difference
);

public sealed record CashSessionInfo(
    Guid Id,
    string TerminalId,
    string OperatorName,
    decimal OpeningAmount,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    string Status
);

public sealed record CashSessionSalesSummary(
    int TotalSales,
    int CancelledSales,
    decimal GrossRevenue,
    decimal Discounts,
    decimal NetRevenue
);

public sealed record PaymentBreakdown(
    string Method,
    int Count,
    decimal Total
);

public sealed record InventoryMovementEntry(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Barcode,
    decimal QuantityDelta,
    string Type,
    string? Notes,
    DateTimeOffset CreatedAt
);
