using Microsoft.EntityFrameworkCore;
using Rincon.DataAccess.Data;
using Rincon.Utilities.Enums;

namespace Rincon.Infrastructure;

// Account adjustments reduce the receivable; cash refunds reduce money collected.
// Allocate net payments FIFO. Payments left after covering the receivable become
// account credit and are automatically applied to later account sales.
public static class AccountLedger
{
    public static IQueryable<AccountSaleBalance> Sales(ApplicationDbContext db)
    {
        var sales = db.DirectSales.AsNoTracking()
            .Where(s => s.PaymentMethod == PaymentMethod.CuentaPersonal && s.PersonalAccountId != null)
            .Select(s => new
            {
                s.Id, AccountId = s.PersonalAccountId!.Value, s.Date,
                Total = s.Total - s.Returns.Sum(r => r.Total)
            });
        return from sale in sales
               let collected = db.PersonalAccountPayments.Where(p => p.PersonalAccountId == sale.AccountId && !p.IsVoided).Sum(p => (decimal?)p.Amount) ?? 0m
               let refunded = db.DirectSaleReturns.Where(r => r.DirectSale.PersonalAccountId == sale.AccountId && r.RefundMethod != PaymentMethod.CuentaPersonal).Sum(r => (decimal?)r.Total) ?? 0m
               let openingBalance = db.PersonalAccounts.Where(a => a.Id == sale.AccountId).Select(a => (decimal?)a.OpeningBalance).FirstOrDefault() ?? 0m
               let older = sales.Where(s => s.AccountId == sale.AccountId && (s.Date < sale.Date || (s.Date == sale.Date && s.Id < sale.Id))).Sum(s => (decimal?)s.Total) ?? 0m
               select new AccountSaleBalance
               {
                   Id = sale.Id, AccountId = sale.AccountId, Date = sale.Date, Total = sale.Total,
                   AmountPaid = Math.Max(0m, Math.Min(sale.Total, collected - refunded - openingBalance - older))
               };
    }

    public static IQueryable<AccountBalance> Accounts(ApplicationDbContext db)
    {
        var sales = Sales(db);
        return from account in db.PersonalAccounts.AsNoTracking()
               let sold = sales.Where(s => s.AccountId == account.Id).Sum(s => (decimal?)s.Total) ?? 0m
               let paid = db.PersonalAccountPayments.Where(p => p.PersonalAccountId == account.Id && !p.IsVoided).Sum(p => (decimal?)p.Amount) ?? 0m
               let refunded = db.DirectSaleReturns.Where(r => r.DirectSale.PersonalAccountId == account.Id && r.RefundMethod != PaymentMethod.CuentaPersonal).Sum(r => (decimal?)r.Total) ?? 0m
               let rawBalance = account.OpeningBalance + sold - paid + refunded
               select new AccountBalance
               {
                   Id = account.Id, FullName = account.FullName, DNI = account.DNI,
                   Phone = account.Phone, Address = account.Address, isActive = account.isActive,
                   DebtValue = Math.Max(0m, rawBalance),
                   CreditValue = Math.Max(0m, -rawBalance),
                   DebtSinceValue = rawBalance <= 0m
                       ? null
                       : account.OpeningBalance > Math.Max(0m, paid - refunded)
                           ? account.Date
                           : sales.Where(s => s.AccountId == account.Id && s.Total > s.AmountPaid).Min(s => (DateTime?)s.Date)
               };
    }
}

public sealed class AccountSaleBalance
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
}

public sealed class AccountBalance
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string DNI { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool isActive { get; set; }
    public decimal DebtValue { get; set; }
    public decimal CreditValue { get; set; }
    public DateTime? DebtSinceValue { get; set; }
}
