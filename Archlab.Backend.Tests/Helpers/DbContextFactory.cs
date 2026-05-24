using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Archlab.Backend.Data;
using Archlab.Backend.Services.Settings;

namespace Archlab.Backend.Tests.Helpers;

public static class DbContextFactory
{
    /// <summary>
    /// Creates a test DbContext backed by an in-memory SQLite database.
    /// The returned tuple includes the context AND the open connection that keeps the DB alive.
    /// The caller must dispose BOTH.
    /// </summary>
    public static (PdvDbContext Db, SqliteConnection Connection) CreateWithConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<PdvDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new PdvDbContext(options);
        db.Database.EnsureCreated();

        // Seed the atomic counter rows required by SaleService and FiscalDocumentService
        if (!db.SaleCounters.Any())
            db.SaleCounters.Add(new Archlab.Backend.Domain.SaleCounter { Id = 1, LastNumber = 0 });

        if (!db.FiscalCounters.Any())
            db.FiscalCounters.Add(new Archlab.Backend.Domain.FiscalCounter { Id = 1, LastNumber = 0 });

        db.SaveChanges();

        return (db, connection);
    }

    public static IOptions<JwtSettings> BuildJwtOptions(
        string secretKey = "test-secret-key-minimum-32-bytes-long-for-hmac-sha256",
        string issuer = "pdv-test",
        string audience = "pdv-test-client",
        int expirationHours = 8)
    {
        return Options.Create(new JwtSettings
        {
            SecretKey = secretKey,
            Issuer = issuer,
            Audience = audience,
            ExpirationHours = expirationHours
        });
    }

    public static NullLogger<T> NullLogger<T>() => new();
}
