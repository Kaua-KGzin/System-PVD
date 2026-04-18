using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pdv.Backend.Contracts;
using Pdv.Backend.Data;

namespace Pdv.Backend.Services;

public sealed class AuthService(PdvDbContext db, IConfiguration config)
{
    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return ServiceResult<LoginResponse>.Fail("Usuario e senha sao obrigatorios.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username.Trim(), cancellationToken);

        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return ServiceResult<LoginResponse>.Fail("Credenciais invalidas.", StatusCodes.Status401Unauthorized);

        var token = GenerateToken(user);
        return ServiceResult<LoginResponse>.Ok(new LoginResponse(token, user.Username, user.Role));
    }

    private string GenerateToken(Domain.User user)
    {
        var secretKey = config["Jwt:SecretKey"]!;
        var issuer = config["Jwt:Issuer"]!;
        var audience = config["Jwt:Audience"]!;
        var expirationHours = config.GetValue<int>("Jwt:ExpirationHours", 8);

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
            expires: DateTime.UtcNow.AddHours(expirationHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
