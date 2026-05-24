namespace Archlab.Backend.Domain;

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    // SHA-256 hash of the raw token — raw value never persisted
    public required string Token { get; set; }

    // Groups all tokens in the same login session chain for theft detection
    public Guid TokenFamily { get; set; } = Guid.NewGuid();

    // Hash of the token that replaced this one (for audit trail)
    public string? ReplacedByToken { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
