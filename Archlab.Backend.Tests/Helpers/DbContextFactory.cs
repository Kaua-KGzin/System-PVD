using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Archlab.Backend.Data;

namespace Archlab.Backend.Tests.Helpers;

public static class DbContextFactory
{
    /// <summary>
    /// Creates a test DbContext backed by an in-memory SQLite database.
    /// The returned tuple includes the context AND the open connection that keeps the DB alive.
    /// The caller must dispose BOTH.
    /// </summary>
    public static (PdvDbContext Db, SqliteConnection Connection) CreateWithConnection(
        IHttpContextAccessor? httpContextAccessor = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<PdvDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new Archlab.Backend.Data.Interceptors.AuditLogInterceptor(httpContextAccessor))
            .Options;


        var db = new PdvDbContext(options);
        db.Database.EnsureCreated();
        return (db, connection);
    }

    public static IConfiguration BuildConfig(
        string jwtKey = "test-secret-key-minimum-32-bytes-long-ok",
        string issuer = "pdv-test",
        string audience = "pdv-test-client")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = jwtKey,
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience,
                ["Jwt:ExpirationHours"] = "8"
            })
            .Build();
    }
}
