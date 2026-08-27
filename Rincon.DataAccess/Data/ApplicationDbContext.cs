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
    public DbSet<PersonalAccount> PersonalAccounts => Set<PersonalAccount>();
    public DbSet<PersonalAccountPayment> PersonalAccountPayments => Set<PersonalAccountPayment>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        foreach (var property in builder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()).Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)))
            property.SetColumnType("timestamp without time zone");

        builder.Entity<Product>(e => { e.Property(p => p.PurchasePrice).HasPrecision(18, 2); e.Property(p => p.SalePrice).HasPrecision(18, 2); });
        builder.Entity<DirectSale>(e => { e.Property(p => p.Total).HasPrecision(18, 2); e.Property(p => p.TotalCost).HasPrecision(18, 2); });
        builder.Entity<DirectSaleItem>(entity =>
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
