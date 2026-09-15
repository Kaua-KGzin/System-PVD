namespace Archlab.Backend.Contracts;

public sealed record SellerCommissionResponse(
    Guid Id,
    Guid UserId,
    string Username,
    decimal Percentage,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CommissionTransactionResponse(
    Guid Id,
    int SaleNumber,
    decimal SaleAmount,
    decimal CommissionPercentage,
    decimal CommissionAmount,
    bool IsPaid,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt);

public sealed record CreateSellerCommissionRequest(
    Guid UserId,
    decimal Percentage);

public sealed record UpdateSellerCommissionRequest(
    decimal? Percentage,
    bool? IsActive);

public sealed record CommissionSummary(
    decimal TotalCommission,
    decimal PaidCommission,
    decimal PendingCommission,
    int TotalSales,
    CommissionTransactionResponse[] RecentTransactions);
