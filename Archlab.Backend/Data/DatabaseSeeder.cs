using Microsoft.EntityFrameworkCore;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PdvDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        await db.Database.MigrateAsync();

        if (!await db.SaleCounters.AnyAsync())
        {
            db.SaleCounters.Add(new SaleCounter { Id = 1, LastNumber = 0 });
        }

        if (!await db.Users.AnyAsync())
        {
            var seedUsername = config["SeedAdmin:Username"];
            var seedPassword = config["SeedAdmin:Password"];
            var seedRole = config["SeedAdmin:Role"] ?? "Admin";

            if (string.IsNullOrWhiteSpace(seedUsername) || string.IsNullOrWhiteSpace(seedPassword))
            {
                throw new InvalidOperationException("No users exist. Configure SeedAdmin:Username and SeedAdmin:Password to bootstrap the first admin.");
            }

            if (!environment.IsDevelopment() && seedPassword == "admin123")
            {
                throw new InvalidOperationException("Refusing to seed the default development admin password outside Development.");
            }

            db.Users.Add(new User
            {
                Username = seedUsername.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword),
                Role = seedRole.Trim()
            });
        }

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                new Product
                {
                    Barcode = "7891000100103",
                    Sku = "MER-ARROZ-5KG",
                    Name = "Arroz Tipo 1 5kg",
                    UnitOfMeasure = "UN",
                    UnitPrice = 24.90m,
                    StockQuantity = 80,
                    MinStockQuantity = 10
                },
                new Product
                {
                    Barcode = "7894900011517",
                    Sku = "BEB-COLA-2L",
                    Name = "Refrigerante Cola 2L",
                    UnitOfMeasure = "UN",
                    UnitPrice = 8.99m,
                    StockQuantity = 120,
                    MinStockQuantity = 24
                },
                new Product
                {
                    Barcode = "7891910000197",
                    Sku = "MER-CAFE-500G",
                    Name = "Cafe Torrado 500g",
                    UnitOfMeasure = "UN",
                    UnitPrice = 18.50m,
                    StockQuantity = 60,
                    MinStockQuantity = 12
                },
                new Product
                {
                    Barcode = "7896004000915",
                    Sku = "LIM-DETERG-500ML",
                    Name = "Detergente Neutro 500ml",
                    UnitOfMeasure = "UN",
                    UnitPrice = 2.79m,
                    StockQuantity = 200,
                    MinStockQuantity = 30
                });
        }

        if (!await db.Suppliers.AnyAsync())
        {
            db.Suppliers.AddRange(
                new Supplier
                {
                    Name = "Distribuidora Central",
                    Cnpj = "00.000.000/0001-00",
                    ContactName = "Comercial",
                    Phone = "(11) 3000-1000",
                    Email = "comercial@central.example"
                },
                new Supplier
                {
                    Name = "Atacado Brasil",
                    Cnpj = "11.111.111/0001-11",
                    ContactName = "Suprimentos",
                    Phone = "(11) 3000-2000",
                    Email = "suprimentos@atacado.example"
                });
        }

        await db.SaveChangesAsync();
    }
}
