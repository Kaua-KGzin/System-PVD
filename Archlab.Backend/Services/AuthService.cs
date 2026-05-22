using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Archlab.Backend.Contracts;
using Archlab.Backend.Data;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Services;

public sealed class AuthService(PdvDbContext db, IConfiguration config)
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return ServiceResult<LoginResponse>.Fail("Usuario e senha sao obrigatorios.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username.Trim(), cancellationToken);

        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return ServiceResult<LoginResponse>.Fail("Credenciais invalidas.", StatusCodes.Status401Unauthorized);

        var (token, expiresAt) = GenerateToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, cancellationToken);

        return ServiceResult<LoginResponse>.Ok(new LoginResponse(token, refreshToken, expiresAt, user.Username, user.Role));
    }

    public async Task<ServiceResult<LoginResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return ServiceResult<LoginResponse>.Fail("Refresh token e obrigatorio.", StatusCodes.Status401Unauthorized);

        var stored = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken, cancellationToken);

        if (stored is null || stored.IsRevoked || stored.ExpiresAt <= DateTimeOffset.UtcNow)
            return ServiceResult<LoginResponse>.Fail("Refresh token invalido ou expirado.", StatusCodes.Status401Unauthorized);

        if (stored.User is null || !stored.User.IsActive)
            return ServiceResult<LoginResponse>.Fail("Usuario inativo.", StatusCodes.Status401Unauthorized);

        stored.IsRevoked = true;

        var (token, expiresAt) = GenerateToken(stored.User);
        var newRefreshToken = await CreateRefreshTokenAsync(stored.User.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<LoginResponse>.Ok(new LoginResponse(token, newRefreshToken, expiresAt, stored.User.Username, stored.User.Role));
    }

    public async Task<ServiceResult<bool>> LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == refreshToken, cancellationToken);

        if (stored is not null && !stored.IsRevoked)
        {
            stored.IsRevoked = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<bool>.Ok(true);
    }

    private (string Token, DateTimeOffset ExpiresAt) GenerateToken(User user)
    {
        var secretKey = config["Jwt:SecretKey"]!;
        var issuer = config["Jwt:Issuer"]!;
        var audience = config["Jwt:Audience"]!;
        var expirationHours = config.GetValue<int>("Jwt:ExpirationHours", 8);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(expirationHours);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(64)).ToLowerInvariant();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiresAt = DateTimeOffset.UtcNow.Add(RefreshTokenLifetime)
        });

        await db.SaveChangesAsync(cancellationToken);

        return token;
    }
}
