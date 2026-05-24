using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;
using Archlab.Backend.Services.Settings;

namespace Archlab.Backend.Services;

public sealed class AuthService(
    PdvDbContext db,
    IOptions<JwtSettings> jwtOptions,
    ILogger<AuthService> logger)
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return ServiceResult<LoginResponse>.Fail("Usuario e senha sao obrigatorios.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username.Trim(), cancellationToken);

        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Login falhou para username={Username}", request.Username.Trim());
            return ServiceResult<LoginResponse>.Fail("Credenciais invalidas.", StatusCodes.Status401Unauthorized);
        }

        var (token, expiresAt) = GenerateJwt(user);

        // Each login starts a new token family (independent session chain)
        var family = Guid.NewGuid();
        var rawRefreshToken = await CreateRefreshTokenAsync(user.Id, family, cancellationToken);

        logger.LogInformation("Login realizado: User={Username} Role={Role}", user.Username, user.Role);

        return ServiceResult<LoginResponse>.Ok(new LoginResponse(token, rawRefreshToken, expiresAt, user.Username, user.Role));
    }

    public async Task<ServiceResult<LoginResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return ServiceResult<LoginResponse>.Fail("Refresh token e obrigatorio.", StatusCodes.Status401Unauthorized);

        var hashedToken = HashToken(request.RefreshToken);

        var stored = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == hashedToken, cancellationToken);

        // Token unknown or expired — safe failure
        if (stored is null || stored.ExpiresAt <= DateTimeOffset.UtcNow)
            return ServiceResult<LoginResponse>.Fail("Refresh token invalido ou expirado.", StatusCodes.Status401Unauthorized);

        // Token already revoked → reuse detected → revoke entire family (token theft indicator)
        if (stored.IsRevoked)
        {
            await db.RefreshTokens
                .Where(r => r.TokenFamily == stored.TokenFamily && !r.IsRevoked)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true), cancellationToken);

            // ExecuteUpdateAsync bypasses the EF change tracker — clear stale cached entities
            // so any subsequent query in the same scope reads fresh values from the database.
            db.ChangeTracker.Clear();

            logger.LogWarning(
                "Reuso de refresh token detectado. Familia {Family} revogada para UserId={UserId}. Possivel roubo de token.",
                stored.TokenFamily, stored.UserId);

            return ServiceResult<LoginResponse>.Fail(
                "Sessao comprometida. Todas as sessoes foram encerradas. Faca login novamente.",
                StatusCodes.Status401Unauthorized);
        }

        if (stored.User is null || !stored.User.IsActive)
            return ServiceResult<LoginResponse>.Fail("Usuario inativo.", StatusCodes.Status401Unauthorized);

        // Rotate: revoke current, issue new token in same family
        stored.IsRevoked = true;

        var (jwtToken, expiresAt) = GenerateJwt(stored.User);
        var newRawToken = await CreateRefreshTokenAsync(stored.User.Id, stored.TokenFamily, cancellationToken);

        // Link for audit trail
        stored.ReplacedByToken = HashToken(newRawToken);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<LoginResponse>.Ok(
            new LoginResponse(jwtToken, newRawToken, expiresAt, stored.User.Username, stored.User.Role));
    }

    public async Task<ServiceResult<bool>> LogoutAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            return ServiceResult<bool>.Ok(true);

        var hashedToken = HashToken(rawRefreshToken);

        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == hashedToken, cancellationToken);

        if (stored is not null && !stored.IsRevoked)
        {
            stored.IsRevoked = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<bool>.Ok(true);
    }

    // Raw token is returned to client; only the hash is stored in the database.
    // If the database is compromised, stored hashes cannot be used directly as tokens.
    private async Task<string> CreateRefreshTokenAsync(Guid userId, Guid family, CancellationToken cancellationToken)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(); // 64 hex chars
        var hashedToken = HashToken(rawToken);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            Token = hashedToken,
            TokenFamily = family,
            ExpiresAt = DateTimeOffset.UtcNow.Add(RefreshTokenLifetime)
        });

        await db.SaveChangesAsync(cancellationToken);

        return rawToken;
    }

    private (string Token, DateTimeOffset ExpiresAt) GenerateJwt(User user)
    {
        var settings = jwtOptions.Value;
        var expiresAt = DateTimeOffset.UtcNow.AddHours(settings.ExpirationHours);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    // SHA-256 of the raw token — constant-time comparison via database index lookup
    public static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
