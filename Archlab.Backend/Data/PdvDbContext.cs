using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Archlab.Backend.Domain;

namespace Archlab.Backend.Data;

public sealed class PdvDbContext(DbContextOptions<PdvDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SaleCounter> SaleCounters => Set<SaleCounter>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<FiscalDocument> FiscalDocuments => Set<FiscalDocument>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseEntry> PurchaseEntries => Set<PurchaseEntry>();
    public DbSet<PurchaseEntryItem> PurchaseEntryItems => Set<PurchaseEntryItem>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnItem> SaleReturnItems => Set<SaleReturnItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CustomerLoyalty> CustomerLoyalties => Set<CustomerLoyalty>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<SellerCommission> SellerCommissions => Set<SellerCommission>();
    public DbSet<CommissionTransaction> CommissionTransactions => Set<CommissionTransaction>();


    private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetConverter = new(
        value => value.ToUniversalTime().Ticks,
        value => new DateTimeOffset(new DateTime(value, DateTimeKind.Utc)));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetConverter = new(
        value => value.HasValue ? value.Value.ToUniversalTime().Ticks : null,
        value => value.HasValue ? new DateTimeOffset(new DateTime(value.Value, DateTimeKind.Utc)) : null);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Username).HasMaxLength(80).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Role).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Token).IsUnique();
            entity.Property(r => r.Token).HasMaxLength(128).IsRequired();
            entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SaleCounter>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.RowVersion).IsConcurrencyToken();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.Timestamp);
            entity.HasIndex(a => a.EntityName);
            entity.Property(a => a.Action).HasMaxLength(40).IsRequired();
            entity.Property(a => a.EntityName).HasMaxLength(80).IsRequired();
            entity.Property(a => a.EntityId).HasMaxLength(80);
            entity.Property(a => a.UserId).HasMaxLength(80);
            entity.Property(a => a.Username).HasMaxLength(120);
        });


        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Name).IsUnique();
            entity.Property(c => c.Name).HasMaxLength(80).IsRequired();
            entity.Property(c => c.Description).HasMaxLength(240);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Document);
            entity.Property(c => c.Name).HasMaxLength(160).IsRequired();
            entity.Property(c => c.Document).HasMaxLength(20);
            entity.Property(c => c.Phone).HasMaxLength(20);
            entity.Property(c => c.Email).HasMaxLength(120);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.HasIndex(product => product.Barcode).IsUnique();
            entity.HasIndex(product => product.Sku).IsUnique();
            entity.Property(product => product.Barcode).HasMaxLength(64).IsRequired();
            entity.Property(product => product.Sku).HasMaxLength(64);
            entity.Property(product => product.Name).HasMaxLength(160).IsRequired();
            entity.Property(product => product.UnitOfMeasure).HasMaxLength(12).IsRequired();
            entity.Property(product => product.UnitPrice).HasPrecision(18, 2);
            entity.Property(product => product.CostPrice).HasPrecision(18, 2);
            entity.Property(product => product.StockQuantity).HasPrecision(18, 3);
            entity.Property(product => product.MinStockQuantity).HasPrecision(18, 3);
            entity.Property(product => product.RowVersion).IsConcurrencyToken();
            entity.HasOne(product => product.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CashSession>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.HasIndex(session => new { session.TerminalId, session.Status });
            entity.Property(session => session.TerminalId).HasMaxLength(40).IsRequired();
            entity.Property(session => session.OperatorName).HasMaxLength(120).IsRequired();
            entity.Property(session => session.OpeningAmount).HasPrecision(18, 2);
            entity.Property(session => session.ExpectedClosingAmount).HasPrecision(18, 2);
            entity.Property(session => session.ClosingAmount).HasPrecision(18, 2);
            entity.Property(session => session.ClosingDifference).HasPrecision(18, 2);
            entity.Property(session => session.ClosingNotes).HasMaxLength(400);
            entity.Property(session => session.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(session => session.RowVersion).IsConcurrencyToken();
        });


        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(sale => sale.Id);
            entity.HasIndex(sale => sale.Number).IsUnique();
            entity.HasIndex(sale => sale.CreatedAt);
            entity.HasIndex(sale => new { sale.Status, sale.CashSessionId });
            entity.Property(sale => sale.TerminalId).HasMaxLength(40).IsRequired();
            entity.Property(sale => sale.OperatorName).HasMaxLength(120).IsRequired();
            entity.Property(sale => sale.CustomerDocument).HasMaxLength(32);
            entity.Property(sale => sale.CancellationReason).HasMaxLength(400);
            entity.Property(sale => sale.GrossTotal).HasPrecision(18, 2);
            entity.Property(sale => sale.ItemDiscountTotal).HasPrecision(18, 2);
            entity.Property(sale => sale.SaleDiscountTotal).HasPrecision(18, 2);
            entity.Property(sale => sale.NetTotal).HasPrecision(18, 2);
            entity.Property(sale => sale.PaidAmount).HasPrecision(18, 2);
            entity.Property(sale => sale.ChangeAmount).HasPrecision(18, 2);
            entity.Property(sale => sale.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasOne(sale => sale.CashSession)
                .WithMany(session => session.Sales)
                .HasForeignKey(sale => sale.CashSessionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(sale => sale.Customer)
                .WithMany(c => c.Sales)
                .HasForeignKey(sale => sale.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Barcode).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ProductName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.UnitOfMeasure).HasMaxLength(12).IsRequired();
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.UnitDiscount).HasPrecision(18, 2);
            entity.Property(item => item.GrossTotal).HasPrecision(18, 2);
            entity.Property(item => item.DiscountTotal).HasPrecision(18, 2);
            entity.Property(item => item.NetTotal).HasPrecision(18, 2);
            entity.HasOne(item => item.Sale)
                .WithMany(sale => sale.Items)
                .HasForeignKey(item => item.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Product)
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalePayment>(entity =>
        {
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Method).HasConversion<string>().HasMaxLength(32);
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.TransactionReference).HasMaxLength(120);
            entity.HasOne(payment => payment.Sale)
                .WithMany(sale => sale.Payments)
                .HasForeignKey(payment => payment.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FiscalDocument>(entity =>
        {
            entity.HasKey(document => document.Id);
            entity.HasIndex(document => document.SaleId).IsUnique();
            entity.HasIndex(document => document.AccessKey).IsUnique();
            entity.Property(document => document.Model).HasMaxLength(16).IsRequired();
            entity.Property(document => document.Series).HasMaxLength(8).IsRequired();
            entity.Property(document => document.AccessKey).HasMaxLength(44).IsRequired();
            entity.Property(document => document.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasOne(document => document.Sale)
                .WithOne(sale => sale.FiscalDocument)
                .HasForeignKey<FiscalDocument>(document => document.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(movement => movement.Id);
            entity.HasIndex(movement => movement.CreatedAt);
            entity.Property(movement => movement.QuantityDelta).HasPrecision(18, 3);
            entity.Property(movement => movement.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(movement => movement.Notes).HasMaxLength(240);
            entity.HasOne(movement => movement.Product)
                .WithMany(product => product.InventoryMovements)
                .HasForeignKey(movement => movement.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.Cnpj).IsUnique();
            entity.Property(s => s.Name).HasMaxLength(160).IsRequired();
            entity.Property(s => s.Cnpj).HasMaxLength(18);
            entity.Property(s => s.ContactName).HasMaxLength(120);
            entity.Property(s => s.Phone).HasMaxLength(20);
            entity.Property(s => s.Email).HasMaxLength(120);
        });

        modelBuilder.Entity<PurchaseEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.InvoiceNumber).HasMaxLength(60).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(400);
            entity.Property(e => e.TotalCost).HasPrecision(18, 2);
            entity.HasOne(e => e.Supplier)
                .WithMany(s => s.PurchaseEntries)
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseEntryItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Quantity).HasPrecision(18, 3);
            entity.Property(i => i.UnitCost).HasPrecision(18, 2);
            entity.Property(i => i.TotalCost).HasPrecision(18, 2);
            entity.HasOne(i => i.PurchaseEntry)
                .WithMany(e => e.Items)
                .HasForeignKey(i => i.PurchaseEntryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CashMovement>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Amount).HasPrecision(18, 2);
            entity.Property(m => m.Reason).HasMaxLength(240);
            entity.Property(m => m.OperatorName).HasMaxLength(120).IsRequired();
            entity.HasOne(m => m.CashSession)
                .WithMany(s => s.Movements)
                .HasForeignKey(m => m.CashSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SaleReturn>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Reason).HasMaxLength(240).IsRequired();
            entity.Property(r => r.OperatorName).HasMaxLength(120).IsRequired();
            entity.Property(r => r.TotalRefundAmount).HasPrecision(18, 2);
            entity.HasOne(r => r.Sale)
                .WithMany(s => s.Returns)
                .HasForeignKey(r => r.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SaleReturnItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Barcode).HasMaxLength(64).IsRequired();
            entity.Property(i => i.ProductName).HasMaxLength(160).IsRequired();
            entity.Property(i => i.Quantity).HasPrecision(18, 3);
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
            entity.Property(i => i.RefundAmount).HasPrecision(18, 2);
            entity.HasOne(i => i.SaleReturn)
                .WithMany(r => r.Items)
                .HasForeignKey(i => i.SaleReturnId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerLoyalty>(entity =>
        {
            entity.HasKey(cl => cl.Id);
            entity.HasIndex(cl => cl.CustomerId).IsUnique();
            entity.HasOne(cl => cl.Customer)
                .WithMany()
                .HasForeignKey(cl => cl.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoyaltyTransaction>(entity =>
        {
            entity.HasKey(lt => lt.Id);
            entity.HasIndex(lt => lt.CreatedAt);
            entity.Property(lt => lt.Type).HasMaxLength(20).IsRequired();
            entity.Property(lt => lt.Notes).HasMaxLength(240);
            entity.HasOne(lt => lt.CustomerLoyalty)
                .WithMany(cl => cl.Transactions)
                .HasForeignKey(lt => lt.CustomerLoyaltyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(lt => lt.Sale)
                .WithMany()
                .HasForeignKey(lt => lt.SaleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SellerCommission>(entity =>
        {
            entity.HasKey(sc => sc.Id);
            entity.HasIndex(sc => sc.UserId).IsUnique();
            entity.Property(sc => sc.Percentage).HasPrecision(5, 2);
            entity.HasOne(sc => sc.User)
                .WithMany()
                .HasForeignKey(sc => sc.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommissionTransaction>(entity =>
        {
            entity.HasKey(ct => ct.Id);
            entity.HasIndex(ct => ct.CreatedAt);
            entity.Property(ct => ct.SaleAmount).HasPrecision(18, 2);
            entity.Property(ct => ct.CommissionPercentage).HasPrecision(5, 2);
            entity.Property(ct => ct.CommissionAmount).HasPrecision(18, 2);
            entity.HasOne(ct => ct.SellerCommission)
                .WithMany(sc => sc.Transactions)
                .HasForeignKey(ct => ct.SellerCommissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ct => ct.Sale)
                .WithMany()
                .HasForeignKey(ct => ct.SaleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SQLite doesn't support DateTimeOffset natively — convert to UTC ticks
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            ConfigureDateTimeOffsetConverters(modelBuilder);
        }
    }

    private static void ConfigureDateTimeOffsetConverters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(DateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(NullableDateTimeOffsetConverter);
                }
            }
        }
    }
}
