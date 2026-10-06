using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rincon.Models;

namespace Rincon.DataAccess.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<DirectSale> DirectSales => Set<DirectSale>();
    public DbSet<DirectSaleItem> DirectSaleItems => Set<DirectSaleItem>();
    public DbSet<DirectSaleReturn> DirectSaleReturns => Set<DirectSaleReturn>();
    public DbSet<DirectSaleReturnItem> DirectSaleReturnItems => Set<DirectSaleReturnItem>();
    public DbSet<PersonalAccount> PersonalAccounts => Set<PersonalAccount>();
    public DbSet<PersonalAccountPayment> PersonalAccountPayments => Set<PersonalAccountPayment>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<PersonalAccountPayment>(entity =>
        {
            entity.HasIndex(p => p.OperationId).IsUnique();
            entity.HasIndex(p => p.ReplacesPaymentId).IsUnique();
            entity.HasOne(p => p.ReplacesPayment)
                .WithMany()
                .HasForeignKey(p => p.ReplacesPaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(p => p.VoidedByUser)
                .WithMany()
                .HasForeignKey(p => p.VoidedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PersonalAccount>().Property(p => p.OpeningBalance).HasPrecision(18, 2);
        builder.Entity<DirectSaleReturn>().HasIndex(p => p.OperationId).IsUnique();
        builder.Entity<Expense>().HasIndex(p => p.OperationId).IsUnique();
        foreach (var property in builder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()).Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
            property.SetColumnType("timestamp without time zone");

        builder.Entity<Product>(e => { e.Property(p => p.PurchasePrice).HasPrecision(18, 2); e.Property(p => p.SalePrice).HasPrecision(18, 2); });
        builder.Entity<DirectSale>(entity =>
        {
            entity.HasIndex(sale => sale.OperationId).IsUnique();
            entity.Property(sale => sale.Total).HasPrecision(18, 2);
            entity.Property(sale => sale.TotalCost).HasPrecision(18, 2);
            entity.Property(sale => sale.CashAmount).HasPrecision(18, 2);
            entity.Property(sale => sale.TransferAmount).HasPrecision(18, 2);
        });
        builder.Entity<DirectSaleItem>(entity =>
        {
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.UnitCost).HasPrecision(18, 2);
            entity.Property(item => item.Subtotal).HasPrecision(18, 2);
        });
        builder.Entity<DirectSaleReturn>(entity =>
        {
            entity.Property(item => item.Total).HasPrecision(18, 2);
            entity.Property(item => item.TotalCost).HasPrecision(18, 2);
            entity.HasOne(item => item.DirectSale)
                .WithMany(sale => sale.Returns)
                .HasForeignKey(item => item.DirectSaleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<DirectSaleReturnItem>(entity =>
        {
            entity.Property(item => item.Quantity).HasPrecision(18, 3);
            entity.Property(item => item.UnitPrice).HasPrecision(18, 2);
            entity.Property(item => item.UnitCost).HasPrecision(18, 2);
            entity.Property(item => item.Subtotal).HasPrecision(18, 2);
        });
        builder.Entity<Expense>().Property(e => e.Amount).HasPrecision(18, 2);
        builder.Entity<PersonalAccountPayment>().Property(e => e.Amount).HasPrecision(18, 2);
    }
}
