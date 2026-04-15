using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pdv.Backend.Domain;

namespace Pdv.Backend.Data;

public sealed class PdvDbContext(DbContextOptions<PdvDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetConverter = new(
        value => value.ToUniversalTime().Ticks,
        value => new DateTimeOffset(new DateTime(value, DateTimeKind.Utc)));

    private static readonly ValueConverter<DateTimeOffset?, long?> NullableDateTimeOffsetConverter = new(
        value => value.HasValue ? value.Value.ToUniversalTime().Ticks : null,
        value => value.HasValue ? new DateTimeOffset(new DateTime(value.Value, DateTimeKind.Utc)) : null);

    public DbSet<Product> Products => Set<Product>();
    public DbSet<CashSession> CashSessions => Set<CashSession>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<FiscalDocument> FiscalDocuments => Set<FiscalDocument>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
            entity.Property(product => product.StockQuantity).HasPrecision(18, 3);
            entity.Property(product => product.MinStockQuantity).HasPrecision(18, 3);
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
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(sale => sale.Id);
            entity.HasIndex(sale => sale.Number).IsUnique();
            entity.HasIndex(sale => sale.CreatedAt);
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

        ConfigureDateTimeOffsetConverters(modelBuilder);
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
