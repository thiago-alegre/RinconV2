using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rincon.DataAccess.Data;
using Rincon.Models.ViewModels;
using Rincon.Utilities;
using Rincon.Utilities.Enums;

namespace Rincon.Areas.Employee.Controllers;

[Area("Employee")]
[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
public class BalanceController : Controller
{
    private readonly ApplicationDbContext _db;
    public BalanceController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(DateTime? dateFrom, DateTime? dateTo)
    {
        var from = (dateFrom ?? DateTime.Today.AddMonths(-1)).Date;
        var to = (dateTo ?? DateTime.Today).Date;
        if (to < from) (from, to) = (to, from);
        var end = to.AddDays(1);

        var salesByPaymentMethod = await _db.DirectSales.AsNoTracking()
            .Where(s => !s.IsVoided && s.Date >= from && s.Date < end && s.PaymentMethod != PaymentMethod.CuentaPersonal)
            .GroupBy(sale => sale.PaymentMethod)
            .Select(group => new
            {
                PaymentMethod = group.Key,
                Total = group.Sum(sale => sale.Total)
            })
            .ToDictionaryAsync(item => item.PaymentMethod, item => item.Total);
        var collectionsByPaymentMethod = await _db.PersonalAccountPayments.AsNoTracking()
            .Where(p => p.Date >= from && p.Date < end)
            .GroupBy(payment => payment.PaymentMethod)
            .Select(group => new
            {
                PaymentMethod = group.Key,
                Total = group.Sum(payment => payment.Amount)
            })
            .ToDictionaryAsync(item => item.PaymentMethod, item => item.Total);
        var accountSales = await _db.DirectSales.AsNoTracking()
            .Where(s => !s.IsVoided && s.PaymentMethod == PaymentMethod.CuentaPersonal)
            .SumAsync(s => (decimal?) s.Total) ?? 0;
        var allCollections = await _db.PersonalAccountPayments.AsNoTracking().SumAsync(p => (decimal?) p.Amount) ?? 0;
        var outstanding = Math.Max(0, accountSales - allCollections);
        var expensesQuery = _db.Expenses
            .AsNoTracking()
            .Where(expense =>
                !expense.IsVoided &&
                expense.Date >= from &&
                expense.Date < end);
        var expenseBreakdown = await expensesQuery
            .GroupBy(expense => new { expense.Type, expense.PaymentMethod })
            .Select(group => new
            {
                group.Key.Type,
                group.Key.PaymentMethod,
                Total = group.Sum(expense => expense.Amount)
            })
            .ToListAsync();
        var expenseTotals = expenseBreakdown
            .GroupBy(item => item.Type)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Total));
        var expensesByPaymentMethod = expenseBreakdown
            .GroupBy(item => item.PaymentMethod)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Total));
        var recentExpenses = await expensesQuery
            .OrderByDescending(expense => expense.Date)
            .ThenByDescending(expense => expense.Id)
            .Take(3)
            .ToListAsync();
        var estimatedProfit = await _db.DirectSales.AsNoTracking()
            .Where(s => !s.IsVoided && s.Date >= from && s.Date < end)
            .SumAsync(s => (decimal?) (s.Total - s.TotalCost)) ?? 0;
        var regularSales = salesByPaymentMethod.Values.Sum();
        var accountCollections = collectionsByPaymentMethod.Values.Sum();
        var availableCash = salesByPaymentMethod.GetValueOrDefault(PaymentMethod.Efectivo)
            + collectionsByPaymentMethod.GetValueOrDefault(PaymentMethod.Efectivo)
            - expensesByPaymentMethod.GetValueOrDefault(PaymentMethod.Efectivo);
        var availableTransfer = salesByPaymentMethod.GetValueOrDefault(PaymentMethod.Transferencia)
            + collectionsByPaymentMethod.GetValueOrDefault(PaymentMethod.Transferencia)
            - expensesByPaymentMethod.GetValueOrDefault(PaymentMethod.Transferencia);

        return View(new BalanceVM
        {
            DateFrom = from,
            DateTo = to,
            CollectedSales = regularSales,
            PersonalAccountCollections = accountCollections,
            OutstandingPersonalAccounts = outstanding,
            MerchandisePurchases = expenseTotals.GetValueOrDefault(ExpenseType.CompraMercaderia),
            BusinessExpenses = expenseTotals.GetValueOrDefault(ExpenseType.GastoNegocio),
            PersonalWithdrawals = expenseTotals.GetValueOrDefault(ExpenseType.RetiroPersonal),
            AvailableCash = availableCash,
            AvailableTransfer = availableTransfer,
            EstimatedProfit = estimatedProfit,
            RecentExpenses = recentExpenses
        });
    }
}
