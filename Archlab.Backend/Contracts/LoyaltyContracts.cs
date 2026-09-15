namespace Archlab.Backend.Contracts;

public sealed record CustomerLoyaltyResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    int TotalPoints,
    int RedeemedPoints,
    int AvailablePoints,
    DateTimeOffset CreatedAt,
    LoyaltyTransactionResponse[] Transactions);

public sealed record LoyaltyTransactionResponse(
    Guid Id,
    int Points,
    string Type,
    string? Notes,
    DateTimeOffset CreatedAt);

public sealed record EarnPointsRequest(
    Guid CustomerId,
    Guid? SaleId,
    decimal Amount);

public sealed record RedeemPointsRequest(
    int Points,
    string? Notes);
