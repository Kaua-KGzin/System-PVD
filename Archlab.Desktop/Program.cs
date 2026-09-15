using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
// The WinForms SDK brings no ASP.NET implicit usings; the Web SDK would have supplied these.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Archlab.Backend;
using Archlab.Backend.Data;

namespace Archlab.Desktop;

internal static class Program
{
    /// <summary>Per-user state: the JWT signing key.</summary>
    internal static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ARCHNEXUS");

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            return Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"O ARCHNEXUS nao conseguiu iniciar.\n\n{ex.Message}",
                "ARCHNEXUS",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int Run()
    {
        Directory.CreateDirectory(DataDirectory);

        var port = FindFreePort();
        var baseUrl = $"http://127.0.0.1:{port}";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production
        });

        var settings = new Dictionary<string, string?>
        {
            ["DatabaseProvider"] = "PostgreSQL",
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=ArchNexus;Username=kgzin;Password=KGzin123",
            ["Jwt:SecretKey"] = LoadOrCreateSigningKey(),
            ["Jwt:Issuer"] = "archlab-desktop",
            ["Jwt:Audience"] = "archlab-desktop",
            ["Jwt:ExpirationHours"] = "8",
            ["SeedAdmin:Username"] = "KGzin",
            ["SeedAdmin:Password"] = "KGzin123",
            ["SeedAdmin:Role"] = "Admin"
        };

        builder.Configuration.AddInMemoryCollection(settings);
        builder.WebHost.UseUrls(baseUrl);

        // The React build ships inside the executable, so there is no wwwroot folder on disk
        // to lose, copy wrong, or edit on a live terminal.
        builder.Environment.WebRootFileProvider = new ManifestEmbeddedFileProvider(
            Assembly.GetExecutingAssembly(), "wwwroot");

        var app = ArchlabApi.Build(builder, httpsRedirection: false, serveStaticFiles: true);

        DatabaseSeeder.SeedAsync(app.Services).GetAwaiter().GetResult();
        app.StartAsync().GetAwaiter().GetResult();

        try
        {
            Application.Run(new MainForm(baseUrl));
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            app.StopAsync(shutdown.Token).GetAwaiter().GetResult();
        }

        return 0;
    }

    /// <summary>
    /// The signing key is generated once per installation and kept beside the database. A key
    /// baked into the executable would be identical on every terminal and forgeable by anyone
    /// holding a copy of the app.
    /// </summary>
    private static string LoadOrCreateSigningKey()
    {
        var keyPath = Path.Combine(DataDirectory, "signing.key");

        if (File.Exists(keyPath))
        {
            var existing = File.ReadAllText(keyPath).Trim();
            if (existing.Length >= 32)
            {
                return existing;
            }
        }

        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        File.WriteAllText(keyPath, key);

        // Owner-only, so another account on a shared terminal cannot read the key and mint tokens.
        try
        {
            var info = new FileInfo(keyPath);
            var security = info.GetAccessControl();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                System.Security.Principal.WindowsIdentity.GetCurrent().User!,
                System.Security.AccessControl.FileSystemRights.FullControl,
                System.Security.AccessControl.AccessControlType.Allow));
            info.SetAccessControl(security);
        }
        catch (Exception)
        {
            // A filesystem that cannot carry the ACL still gets the key; it is no worse than
            // the default inherited permissions, and refusing to start would be worse than both.
        }

        return key;
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
