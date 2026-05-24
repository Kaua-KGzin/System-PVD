using System.ComponentModel.DataAnnotations;

namespace Archlab.Backend.Services.Settings;

public sealed class JwtSettings
{
    public const string Section = "Jwt";

    [Required, MinLength(32)]
    public string SecretKey { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 72)]
    public int ExpirationHours { get; init; } = 8;
}
