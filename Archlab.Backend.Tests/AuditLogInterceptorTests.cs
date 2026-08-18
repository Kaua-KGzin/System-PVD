using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Domain;
using Archlab.Backend.Tests.Helpers;

namespace Archlab.Backend.Tests;

public sealed class AuditLogInterceptorTests : IDisposable
{
    private readonly Archlab.Backend.Data.PdvDbContext _db;
    private readonly SqliteConnection _connection;

    public AuditLogInterceptorTests()
    {
        (_db, _connection) = DbContextFactory.CreateWithConnection();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Creating_a_user_never_writes_the_password_hash_to_the_audit_trail()
    {
        _db.Users.Add(new User
        {
            Username = "caixa01",
            PasswordHash = "$2a$11$SUPERSECRETHASHVALUE",
            Role = "Operator"
        });
        await _db.SaveChangesAsync();

        var log = await _db.AuditLogs.SingleAsync(a => a.EntityName == nameof(User));
        Assert.DoesNotContain("SUPERSECRETHASHVALUE", log.ChangesJson);
        Assert.Contains("***REDACTED***", log.ChangesJson);
        // Non-sensitive columns must survive redaction, or the trail is useless.
        Assert.Contains("caixa01", log.ChangesJson);
    }

    [Fact]
    public async Task Issuing_a_refresh_token_never_writes_the_token_to_the_audit_trail()
    {
        var user = new User { Username = "caixa02", PasswordHash = "hash", Role = "Operator" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = "LIVE-REFRESH-TOKEN-VALUE",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();

        var log = await _db.AuditLogs.SingleAsync(a => a.EntityName == nameof(RefreshToken));
        Assert.DoesNotContain("LIVE-REFRESH-TOKEN-VALUE", log.ChangesJson);
        Assert.Contains("***REDACTED***", log.ChangesJson);
    }

    [Fact]
    public async Task Audit_entries_record_the_acting_user_and_ip()
    {
        var userId = Guid.NewGuid().ToString();
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId),
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName, "gerente")
            ],
            authenticationType: "Test",
            nameType: System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName,
            roleType: ClaimTypes.Role));
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.1.2.3");

        var (db, connection) = DbContextFactory.CreateWithConnection(
            new HttpContextAccessor { HttpContext = httpContext });
        using (db)
        using (connection)
        {
            db.Categories.Add(new Category { Name = "Mercearia" });
            await db.SaveChangesAsync();

            var log = await db.AuditLogs.SingleAsync(a => a.EntityName == nameof(Category));
            Assert.Equal(userId, log.UserId);
            Assert.Equal("gerente", log.Username);
            Assert.Equal("10.1.2.3", log.IpAddress);
        }
    }

    [Fact]
    public async Task Audit_entries_outside_a_request_leave_the_actor_null()
    {
        _db.Categories.Add(new Category { Name = "Bebidas" });
        await _db.SaveChangesAsync();

        var log = await _db.AuditLogs.SingleAsync(a => a.EntityName == nameof(Category));
        Assert.Null(log.UserId);
        Assert.Null(log.Username);
        Assert.Null(log.IpAddress);
    }
}
