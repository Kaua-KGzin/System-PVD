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
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseSeeder");

        var hasMigrations = db.Database.GetMigrations().Any();
        if (hasMigrations)
        {
            logger.LogInformation("Applying pending migrations...");
            await db.Database.MigrateAsync();
        }
        else
        {
            logger.LogInformation("No migration files found. Using EnsureCreated to build schema from model.");
            await db.Database.EnsureCreatedAsync();
        }

        if (!await db.SaleCounters.AnyAsync())
        {
            db.SaleCounters.Add(new SaleCounter { Id = 1, LastNumber = 0 });
        }

        if (!await db.Users.AnyAsync())
        {
            var seedUsername = config["SeedAdmin:Username"] ?? "admin";
            var seedPassword = config["SeedAdmin:Password"] ?? "admin123";
            var seedRole = config["SeedAdmin:Role"] ?? "Admin";

            db.Users.Add(new User
            {
                Username = seedUsername.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword),
                Role = seedRole.Trim()
            });
        }

        // Generate Demo Data if it doesn't exist
        if (!await db.Categories.AnyAsync())
        {
            var catMercearia = new Category { Name = "Mercearia", Description = "Produtos de mercearia em geral" };
            var catBebidas = new Category { Name = "Bebidas", Description = "Refrigerantes, sucos e águas" };
            var catLimpeza = new Category { Name = "Limpeza", Description = "Produtos de limpeza" };
            var catPadaria = new Category { Name = "Padaria", Description = "Pães e doces" };
            var catCongelados = new Category { Name = "Congelados", Description = "Carnes e pratos prontos" };
            
            db.Categories.AddRange(catMercearia, catBebidas, catLimpeza, catPadaria, catCongelados);
            await db.SaveChangesAsync();

            var products = new List<Product>
            {
                new Product { CategoryId = catMercearia.Id, Barcode = "7891000100103", Sku = "MER-ARROZ-5KG", Name = "Arroz Tipo 1 5kg", UnitOfMeasure = "UN", UnitPrice = 24.90m, CostPrice = 18.00m, StockQuantity = 80, MinStockQuantity = 20 },
                new Product { CategoryId = catMercearia.Id, Barcode = "7891910000197", Sku = "MER-CAFE-500G", Name = "Cafe Torrado 500g", UnitOfMeasure = "UN", UnitPrice = 18.50m, CostPrice = 12.00m, StockQuantity = 60, MinStockQuantity = 15 },
                new Product { CategoryId = catMercearia.Id, Barcode = "7891010101010", Sku = "MER-FEIJAO-1KG", Name = "Feijao Carioca 1kg", UnitOfMeasure = "UN", UnitPrice = 8.50m, CostPrice = 5.00m, StockQuantity = 100, MinStockQuantity = 30 },
                new Product { CategoryId = catMercearia.Id, Barcode = "7891020202020", Sku = "MER-MACAR-500G", Name = "Macarrao Espaguete 500g", UnitOfMeasure = "UN", UnitPrice = 4.20m, CostPrice = 2.50m, StockQuantity = 150, MinStockQuantity = 40 },
                new Product { CategoryId = catBebidas.Id, Barcode = "7894900011517", Sku = "BEB-COLA-2L", Name = "Refrigerante Cola 2L", UnitOfMeasure = "UN", UnitPrice = 8.99m, CostPrice = 6.00m, StockQuantity = 120, MinStockQuantity = 30 },
                new Product { CategoryId = catBebidas.Id, Barcode = "7894900022222", Sku = "BEB-SUCO-1L", Name = "Suco de Laranja 1L", UnitOfMeasure = "UN", UnitPrice = 6.50m, CostPrice = 4.00m, StockQuantity = 90, MinStockQuantity = 20 },
                new Product { CategoryId = catBebidas.Id, Barcode = "7894900033333", Sku = "BEB-AGUA-500ML", Name = "Agua Mineral 500ml", UnitOfMeasure = "UN", UnitPrice = 2.00m, CostPrice = 1.00m, StockQuantity = 300, MinStockQuantity = 50 },
                new Product { CategoryId = catBebidas.Id, Barcode = "7894900044444", Sku = "BEB-CERV-350ML", Name = "Cerveja Pilsen 350ml", UnitOfMeasure = "UN", UnitPrice = 3.50m, CostPrice = 2.20m, StockQuantity = 400, MinStockQuantity = 100 },
                new Product { CategoryId = catLimpeza.Id, Barcode = "7896004000915", Sku = "LIM-DETERG-500ML", Name = "Detergente Neutro 500ml", UnitOfMeasure = "UN", UnitPrice = 2.79m, CostPrice = 1.50m, StockQuantity = 200, MinStockQuantity = 40 },
                new Product { CategoryId = catLimpeza.Id, Barcode = "7896004000111", Sku = "LIM-SABAO-1KG", Name = "Sabao em Po 1kg", UnitOfMeasure = "UN", UnitPrice = 14.90m, CostPrice = 9.00m, StockQuantity = 80, MinStockQuantity = 20 },
                new Product { CategoryId = catLimpeza.Id, Barcode = "7896004000222", Sku = "LIM-AMAC-2L", Name = "Amaciante 2L", UnitOfMeasure = "UN", UnitPrice = 12.50m, CostPrice = 8.00m, StockQuantity = 60, MinStockQuantity = 15 },
                new Product { CategoryId = catPadaria.Id, Barcode = "7897000011111", Sku = "PAD-PAO-FRANCES", Name = "Pao Frances (KG)", UnitOfMeasure = "KG", UnitPrice = 16.90m, CostPrice = 8.00m, StockQuantity = 50, MinStockQuantity = 10 },
                new Product { CategoryId = catPadaria.Id, Barcode = "7897000022222", Sku = "PAD-BOLO-CHOC", Name = "Bolo de Chocolate", UnitOfMeasure = "UN", UnitPrice = 22.00m, CostPrice = 10.00m, StockQuantity = 8, MinStockQuantity = 2 },
                new Product { CategoryId = catCongelados.Id, Barcode = "7898000011111", Sku = "CON-FRANGO-1KG", Name = "Peito de Frango 1kg", UnitOfMeasure = "KG", UnitPrice = 19.90m, CostPrice = 14.00m, StockQuantity = 40, MinStockQuantity = 10 },
                new Product { CategoryId = catCongelados.Id, Barcode = "7898000022222", Sku = "CON-PIZZA-CALA", Name = "Pizza Calabresa Congelada", UnitOfMeasure = "UN", UnitPrice = 15.90m, CostPrice = 10.00m, StockQuantity = 30, MinStockQuantity = 10 },
            };
            db.Products.AddRange(products);
            await db.SaveChangesAsync();

            // Suppliers
            var sup1 = new Supplier { Name = "Distribuidora Central", Cnpj = "00.000.000/0001-00", ContactName = "Comercial", Phone = "(11) 3000-1000", Email = "comercial@central.example" };
            var sup2 = new Supplier { Name = "Atacado Brasil", Cnpj = "11.111.111/0001-11", ContactName = "Suprimentos", Phone = "(11) 3000-2000", Email = "suprimentos@atacado.example" };
            db.Suppliers.AddRange(sup1, sup2);

            // Customers
            var cust1 = new Customer { Name = "João Silva", Document = "11122233344", Phone = "11999998888", Email = "joao@example.com" };
            var cust2 = new Customer { Name = "Maria Oliveira", Document = "55566677788", Phone = "11977776666", Email = "maria@example.com" };
            db.Customers.AddRange(cust1, cust2);

            await db.SaveChangesAsync();

            // Generate Historical Sales (last 30 days)
            var rand = new Random(42);
            var counter = await db.SaleCounters.FirstAsync();
            var movements = new List<InventoryMovement>();
            
            // To pass foreign key checks, sale needs a CashSession
            var demoSession = new CashSession
            {
                TerminalId = "CAIXA-01",
                OperatorName = "admin",
                OpeningAmount = 100m,
                OpenedAt = DateTimeOffset.UtcNow.AddDays(-31),
                Status = Archlab.Backend.Domain.CashSessionStatus.Open
            };
            db.CashSessions.Add(demoSession);
            await db.SaveChangesAsync();

            for (int i = 30; i >= 0; i--)
            {
                var salesPerDay = rand.Next(2, 8); // 2 to 8 sales a day
                for (int s = 0; s < salesPerDay; s++)
                {
                    counter.LastNumber++;
                    var date = DateTimeOffset.UtcNow.AddDays(-i).AddHours(rand.Next(-8, 8));
                    
                    var sale = new Sale
                    {
                        Number = counter.LastNumber,
                        TerminalId = "CAIXA-01",
                        CashSessionId = demoSession.Id,
                        OperatorName = "admin",
                        CreatedAt = date,
                        Status = Archlab.Backend.Domain.SaleStatus.Completed,
                        Items = new List<SaleItem>(),
                        Payments = new List<SalePayment>()
                    };

                    decimal total = 0;
                    int itemsCount = rand.Next(1, 5);
                    for (int j = 0; j < itemsCount; j++)
                    {
                        var p = products[rand.Next(products.Count)];
                        var qty = rand.Next(1, 4);
                        var itemTotal = qty * p.UnitPrice;
                        total += itemTotal;

                        sale.Items.Add(new SaleItem
                        {
                            ProductId = p.Id,
                            Barcode = p.Barcode,
                            ProductName = p.Name,
                            UnitOfMeasure = p.UnitOfMeasure,
                            Quantity = qty,
                            UnitPrice = p.UnitPrice,
                            UnitDiscount = 0,
                            GrossTotal = itemTotal,
                            DiscountTotal = 0,
                            NetTotal = itemTotal
                        });

                        movements.Add(new InventoryMovement
                        {
                            ProductId = p.Id,
                            QuantityDelta = -qty,
                            Type = Archlab.Backend.Domain.InventoryMovementType.Sale,
                            Notes = $"Venda #{sale.Number}",
                            CreatedAt = date
                        });
                    }

                    sale.GrossTotal = total;
                    sale.NetTotal = total;
                    sale.PaidAmount = total;
                    sale.ChangeAmount = 0;
                    
                    sale.Payments.Add(new SalePayment
                    {
                        Method = rand.Next(2) == 0 ? Archlab.Backend.Domain.PaymentMethod.CreditCard : Archlab.Backend.Domain.PaymentMethod.Pix,
                        Amount = total
                    });

                    db.Sales.Add(sale);
                }
            }

            db.InventoryMovements.AddRange(movements);
            await db.SaveChangesAsync();
        }
    }
}
