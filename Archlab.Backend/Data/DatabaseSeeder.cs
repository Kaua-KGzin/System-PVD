using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Data;

public static class DatabaseSeeder
{
    // Applies pending migrations and seeds initial data.
    // In production this should be called from a pre-deploy migration job,
    // not on every application startup. Configure via environment:
    //   APPLY_MIGRATIONS=true → applies migrations (deploy job)
    //   APPLY_MIGRATIONS=false/unset → skips migrations (normal startup)
    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PdvDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PdvDbContext>>();

        var applyMigrations = config.GetValue<bool>("ApplyMigrations");

        if (applyMigrations || environment.IsDevelopment())
        {
            logger.LogInformation("Aplicando migrations pendentes...");
            await db.Database.MigrateAsync();
            logger.LogInformation("Migrations aplicadas.");
        }
        else
        {
            // Verify connectivity without applying migrations
            await db.Database.CanConnectAsync();
        }

        await SeedCountersAsync(db);
        await SeedAdminUserAsync(db, config, environment, logger);
        await SeedSampleDataAsync(db, environment);

        await db.SaveChangesAsync();
    }

    private static async Task SeedCountersAsync(PdvDbContext db)
    {
        if (!await db.SaleCounters.AnyAsync())
            db.SaleCounters.Add(new SaleCounter { Id = 1, LastNumber = 0 });

        if (!await db.FiscalCounters.AnyAsync())
            db.FiscalCounters.Add(new FiscalCounter { Id = 1, LastNumber = 0 });
    }

    private static async Task SeedAdminUserAsync(
        PdvDbContext db,
        IConfiguration config,
        IHostEnvironment environment,
        ILogger logger)
    {
        if (await db.Users.AnyAsync())
            return;

        var seedUsername = config["SeedAdmin:Username"];
        var seedPassword = config["SeedAdmin:Password"];
        var seedRole = config["SeedAdmin:Role"] ?? "Admin";

        if (string.IsNullOrWhiteSpace(seedUsername) || string.IsNullOrWhiteSpace(seedPassword))
            throw new InvalidOperationException(
                "Nenhum usuario existe. Configure SeedAdmin:Username e SeedAdmin:Password para criar o primeiro admin.");

        if (!environment.IsDevelopment() && seedPassword == "admin123")
            throw new InvalidOperationException(
                "Recusando senha padrao de desenvolvimento fora do ambiente Development.");

        db.Users.Add(new User
        {
            Username = seedUsername.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword),
            Role = seedRole.Trim()
        });

        logger.LogInformation("Usuario admin inicial criado: {Username}", seedUsername.Trim());
    }

    private static async Task SeedSampleDataAsync(PdvDbContext db, IHostEnvironment environment)
    {
        // Sample data only in Development — never seed demo data in production
        if (!environment.IsDevelopment())
            return;

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                new Product { Barcode = "7891000100103", Sku = "MER-ARROZ-5KG", Name = "Arroz Tipo 1 5kg", UnitOfMeasure = "UN", UnitPrice = 24.90m, StockQuantity = 80, MinStockQuantity = 10 },
                new Product { Barcode = "7894900011517", Sku = "BEB-COLA-2L", Name = "Refrigerante Cola 2L", UnitOfMeasure = "UN", UnitPrice = 8.99m, StockQuantity = 120, MinStockQuantity = 24 },
                new Product { Barcode = "7891910000197", Sku = "MER-CAFE-500G", Name = "Cafe Torrado 500g", UnitOfMeasure = "UN", UnitPrice = 18.50m, StockQuantity = 60, MinStockQuantity = 12 },
                new Product { Barcode = "7896004000915", Sku = "LIM-DETERG-500ML", Name = "Detergente Neutro 500ml", UnitOfMeasure = "UN", UnitPrice = 2.79m, StockQuantity = 200, MinStockQuantity = 30 });
        }

        if (!await db.Suppliers.AnyAsync())
        {
            db.Suppliers.AddRange(
                new Supplier { Name = "Distribuidora Central", Cnpj = "11.222.333/0001-81", ContactName = "Comercial", Phone = "(11) 3000-1000", Email = "comercial@central.example" },
                new Supplier { Name = "Atacado Brasil", Cnpj = "22.333.444/0001-97", ContactName = "Suprimentos", Phone = "(11) 3000-2000", Email = "suprimentos@atacado.example" });
        }
    }
}
