using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rincon.DataAccess.Data;
using Rincon.Models.ViewModels;
using Rincon.Utilities;
using Rincon.Infrastructure;
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
        var end = BusinessInput.ExclusiveEnd(to);

        var salesTotals = await _db.DirectSales
            .AsNoTracking()
            .Where(sale =>
                sale.Date >= from &&
                sale.Date < end &&
                sale.PaymentMethod != PaymentMethod.CuentaPersonal)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Cash = group.Sum(sale =>
                    sale.PaymentMethod == PaymentMethod.Efectivo
                        ? sale.Total
                        : sale.PaymentMethod == PaymentMethod.Combinado
                            ? sale.CashAmount
                            : 0),
                Transfer = group.Sum(sale =>
                    sale.PaymentMethod == PaymentMethod.Transferencia
                        ? sale.Total
                        : sale.PaymentMethod == PaymentMethod.Combinado
                            ? sale.TransferAmount
                            : 0)
            })
            .FirstOrDefaultAsync();
        var collectionsByPaymentMethod = await _db.PersonalAccountPayments.AsNoTracking()
            .Where(p => !p.IsVoided && p.Date >= from && p.Date < end)
            .GroupBy(payment => payment.PaymentMethod)
            .Select(group => new
            {
                PaymentMethod = group.Key,
                Total = group.Sum(payment => payment.Amount)
            })
            .ToDictionaryAsync(item => item.PaymentMethod, item => item.Total);
        var returnTotals = await _db.DirectSaleReturns.AsNoTracking()
            .Where(item => item.Date >= from && item.Date < end)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Cash = group.Sum(item =>
                    item.DirectSale.PaymentMethod == PaymentMethod.Efectivo
                        ? item.Total
                        : item.DirectSale.PaymentMethod == PaymentMethod.Combinado && item.DirectSale.Total > 0
                            ? item.Total * item.DirectSale.CashAmount / item.DirectSale.Total
                            : 0),
                Transfer = group.Sum(item =>
                    item.DirectSale.PaymentMethod == PaymentMethod.Transferencia
                        ? item.Total
                        : item.DirectSale.PaymentMethod == PaymentMethod.Combinado && item.DirectSale.Total > 0
                            ? item.Total * item.DirectSale.TransferAmount / item.DirectSale.Total
                            : 0)
            })
            .FirstOrDefaultAsync();
        var accountBalances = AccountLedger.Accounts(_db);
        var outstanding = await accountBalances.SumAsync(a => a.DebtValue);
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
            .Where(s => s.Date >= from && s.Date < end)
            .SumAsync(s => (decimal?) (s.Total - s.TotalCost)) ?? 0;
        var returnedProfit = await _db.DirectSaleReturns.AsNoTracking()
            .Where(item => item.Date >= from && item.Date < end)
            .SumAsync(item => (decimal?) (item.Total - item.TotalCost)) ?? 0;
        var cashSales = salesTotals?.Cash ?? 0;
        var transferSales = salesTotals?.Transfer ?? 0;
        var cashReturns = returnTotals?.Cash ?? 0;
        var transferReturns = returnTotals?.Transfer ?? 0;
        var regularSales = cashSales + transferSales - cashReturns - transferReturns;
        var accountCollections = collectionsByPaymentMethod.Values.Sum();
        var availableCash = cashSales
            + collectionsByPaymentMethod.GetValueOrDefault(PaymentMethod.Efectivo)
            - cashReturns
            - expensesByPaymentMethod.GetValueOrDefault(PaymentMethod.Efectivo);
        var availableTransfer = transferSales
            + collectionsByPaymentMethod.GetValueOrDefault(PaymentMethod.Transferencia)
            - transferReturns
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
            EstimatedProfit = estimatedProfit - returnedProfit,
            RecentExpenses = recentExpenses
        });
    }
}
