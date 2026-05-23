using Archlab.Backend.Domain;

namespace Archlab.Backend.Contracts;

public sealed record CreateSaleRequest(
    Guid CashSessionId,
    string OperatorName,
    string? CustomerDocument,
    Guid? CustomerId,
    decimal SaleDiscountTotal,
    IReadOnlyList<CreateSaleItemRequest> Items,
    IReadOnlyList<CreatePaymentRequest> Payments,
    bool IssueFiscalDocument = false);

public sealed record CreateSaleItemRequest(
    string Barcode,
    decimal Quantity,
    decimal UnitDiscount = 0);

public sealed record CreatePaymentRequest(
    PaymentMethod Method,
    decimal Amount,
    string? TransactionReference = null);

public sealed record CancelSaleRequest(string Reason);

public sealed record SaleResponse(
    Guid Id,
    int Number,
    Guid CashSessionId,
    string TerminalId,
    string OperatorName,
    string? CustomerDocument,
    Guid? CustomerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    decimal GrossTotal,
    decimal ItemDiscountTotal,
    decimal SaleDiscountTotal,
    decimal NetTotal,
    decimal PaidAmount,
    decimal ChangeAmount,
    SaleStatus Status,
    IReadOnlyList<SaleItemResponse> Items,
    IReadOnlyList<SalePaymentResponse> Payments,
    FiscalDocumentResponse? FiscalDocument)
{
    public static SaleResponse From(Sale sale) =>
        new(
            sale.Id,
            sale.Number,
            sale.CashSessionId,
            sale.TerminalId,
            sale.OperatorName,
            sale.CustomerDocument,
            sale.CustomerId,
            sale.CreatedAt,
            sale.CancelledAt,
            sale.CancellationReason,
            sale.GrossTotal,
            sale.ItemDiscountTotal,
            sale.SaleDiscountTotal,
            sale.NetTotal,
            sale.PaidAmount,
            sale.ChangeAmount,
            sale.Status,
            sale.Items.Select(SaleItemResponse.From).ToArray(),
            sale.Payments.Select(SalePaymentResponse.From).ToArray(),
            sale.FiscalDocument is null ? null : FiscalDocumentResponse.From(sale.FiscalDocument));
}

public sealed record SaleItemResponse(
    Guid Id,
    Guid ProductId,
    string Barcode,
    string ProductName,
    string UnitOfMeasure,
    decimal Quantity,
    decimal UnitPrice,
    decimal UnitDiscount,
    decimal GrossTotal,
    decimal DiscountTotal,
    decimal NetTotal)
{
    public static SaleItemResponse From(SaleItem item) =>
        new(
            item.Id,
            item.ProductId,
            item.Barcode,
            item.ProductName,
            item.UnitOfMeasure,
            item.Quantity,
            item.UnitPrice,
            item.UnitDiscount,
            item.GrossTotal,
            item.DiscountTotal,
            item.NetTotal);
}

public sealed record SalePaymentResponse(
    Guid Id,
    PaymentMethod Method,
    decimal Amount,
    string? TransactionReference)
{
    public static SalePaymentResponse From(SalePayment payment) =>
        new(payment.Id, payment.Method, payment.Amount, payment.TransactionReference);
}
