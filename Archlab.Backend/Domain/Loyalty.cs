namespace Archlab.Backend.Domain;

public sealed class CustomerLoyalty
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int TotalPoints { get; set; }
    public int RedeemedPoints { get; set; }
    public int AvailablePoints => TotalPoints - RedeemedPoints;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<LoyaltyTransaction> Transactions { get; set; } = [];
}

public sealed class LoyaltyTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerLoyaltyId { get; set; }
    public CustomerLoyalty CustomerLoyalty { get; set; } = null!;
    public Guid? SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int Points { get; set; }
    public string Type { get; set; } = "Earn"; // Earn, Redeem, Adjust
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
