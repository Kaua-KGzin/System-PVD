namespace Archlab.Backend.Domain;

public sealed class SellerCommission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public decimal Percentage { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<CommissionTransaction> Transactions { get; set; } = [];
}

public sealed class CommissionTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SellerCommissionId { get; set; }
    public SellerCommission SellerCommission { get; set; } = null!;
    public Guid SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public decimal SaleAmount { get; set; }
    public decimal CommissionPercentage { get; set; }
    public decimal CommissionAmount { get; set; }
    public bool IsPaid { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
