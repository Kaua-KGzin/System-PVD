using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Archlab.Backend;
using Archlab.Backend.Data;

namespace Archlab.Backend.Tests.Integration;

/// <summary>
/// Hosts the real API composition — <see cref="ArchlabApi.Build"/>, the same call the web and
/// desktop deployments make — over an in-memory SQLite database, reached through an in-memory
/// TestServer rather than a socket.
/// </summary>
/// <remarks>
/// The service tests construct services directly and so never exercise authentication, the
/// authorization policies, the rate limiter, the security headers, or the two flags that separate
/// the deployment shapes. Everything here exists to cover that gap, which means the composition
/// must be built for real: nothing is stubbed except the database file.
/// </remarks>
public sealed class ApiFactory : IAsyncDisposable
{
    public const string AdminUsername = "admin";
    public const string AdminPassword = "Adm1n!Integration";

    public const string JwtSecretKey = "integration-secret-key-at-least-32-bytes";
    public const string JwtIssuer = "archlab-test";
    public const string JwtAudience = "archlab-test-client";

    // Every request resolves PdvDbContext from its own scope, so a plain ":memory:" database
    // would be discarded between requests along with its connection. A shared-cache database
    // survives as long as one connection stays open, which is what this field is for.
    private readonly SqliteConnection _keepAlive;
    private readonly WebApplication _app;
    private readonly string? _webRoot;

    private ApiFactory(WebApplication app, SqliteConnection keepAlive, string? webRoot, HttpClient client)
    {
        _app = app;
        _keepAlive = keepAlive;
        _webRoot = webRoot;
        Client = client;
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => _app.Services;

    /// <param name="serveStaticFiles">
    /// When true a throwaway web root holding an index.html is created, so the SPA fallback has
    /// something to fall back to — the desktop shape serves the UI from the same origin.
    /// </param>
    public static async Task<ApiFactory> CreateAsync(
        bool httpsRedirection = false,
        bool serveStaticFiles = false,
        // Environments.Development is a static field, not a constant, so it cannot be a default.
        string environment = "Development",
        IDictionary<string, string?>? extraSettings = null)
    {
        var databaseName = $"archnexus-tests-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";

        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        string? webRoot = null;
        if (serveStaticFiles)
        {
            webRoot = Directory.CreateTempSubdirectory(databaseName).FullName;
            await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), SpaShell);
        }

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = environment,
                WebRootPath = webRoot
            });

            var settings = new Dictionary<string, string?>
            {
                ["DatabaseProvider"] = "Sqlite",
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:SecretKey"] = JwtSecretKey,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Jwt:ExpirationHours"] = "8",
                ["SeedAdmin:Username"] = AdminUsername,
                ["SeedAdmin:Password"] = AdminPassword,
                ["SeedAdmin:Role"] = "Admin"
            };

            if (extraSettings is not null)
            {
                foreach (var (key, value) in extraSettings)
                {
                    settings[key] = value;
                }
            }

            builder.Configuration.AddInMemoryCollection(settings);
            builder.WebHost.UseTestServer();

            var app = ArchlabApi.Build(builder, httpsRedirection, serveStaticFiles);

            await DatabaseSeeder.SeedAsync(app.Services);
            await app.StartAsync();

            return new ApiFactory(app, keepAlive, webRoot, app.GetTestClient());
        }
        catch
        {
            await keepAlive.DisposeAsync();
            DeleteWebRoot(webRoot);
            throw;
        }
    }

    /// <summary>Logs in and returns a bearer token for a user that already exists.</summary>
    public async Task<string> TokenForAsync(string username, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        return body!.Token;
    }

    public Task<string> AdminTokenAsync() => TokenForAsync(AdminUsername, AdminPassword);

    /// <summary>Runs work against the host's own database, in its own scope.</summary>
    public async Task WithDbAsync(Func<PdvDbContext, Task> work)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<PdvDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        Client.Dispose();
        await _app.DisposeAsync();
        await _keepAlive.DisposeAsync();
        DeleteWebRoot(_webRoot);
    }

    private static void DeleteWebRoot(string? webRoot)
    {
        if (webRoot is null) return;

        try
        {
            Directory.Delete(webRoot, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }

    private const string SpaShell = "<!doctype html><title>ARCHNEXUS</title><div id=\"root\"></div>";

    private sealed record LoginBody(string Token, string RefreshToken, string Username, string Role);
}
