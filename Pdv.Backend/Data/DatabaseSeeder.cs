using Microsoft.EntityFrameworkCore;
using Pdv.Backend.Domain;

namespace Pdv.Backend.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PdvDbContext>();

        await db.Database.MigrateAsync();

        if (!await db.SaleCounters.AnyAsync())
        {
            db.SaleCounters.Add(new SaleCounter { Id = 1, LastNumber = 0 });
        }

        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin"
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

        await db.SaveChangesAsync();
    }
}
