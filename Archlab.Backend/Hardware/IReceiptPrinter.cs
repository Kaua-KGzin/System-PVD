namespace Archlab.Backend.Hardware;

public interface IReceiptPrinter
{
    Task PrintSaleReceiptAsync(SalePrintModel sale, CancellationToken ct = default);
    Task OpenCashDrawerAsync(CancellationToken ct = default);
    Task PrintCashMovementReceiptAsync(CashMovementPrintModel movement, CancellationToken ct = default);
}

public sealed record SalePrintModel(
    int Number,
    string TerminalId,
    string OperatorName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SaleItemPrintModel> Items,
    decimal GrossTotal,
    decimal DiscountTotal,
    decimal NetTotal,
    decimal PaidAmount,
    decimal ChangeAmount,
    IReadOnlyList<SalePaymentPrintModel> Payments);

public sealed record SaleItemPrintModel(
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    decimal NetTotal);

public sealed record SalePaymentPrintModel(
    string Method,
    decimal Amount);

public sealed record CashMovementPrintModel(
    string Type,
    decimal Amount,
    string? Reason,
    string OperatorName,
    string TerminalId,
    DateTimeOffset CreatedAt);
