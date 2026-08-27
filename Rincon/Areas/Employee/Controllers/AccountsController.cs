using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rincon.DataAccess.Data;
using Rincon.Infrastructure;
using Rincon.Models;
using Rincon.Models.ViewModels;
using Rincon.Utilities;
using Rincon.Utilities.Enums;
using System.Security.Claims;

namespace Rincon.Areas.Employee.Controllers;

[Area("Employee"), Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
public class AccountsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AccountsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var request = DataTableRequest.From(Request);
        var totalsQuery = _db.PersonalAccounts
            .AsNoTracking()
            .Select(account => new
            {
                account.Id,
                account.FullName,
                account.DNI,
                account.Phone,
                account.Address,
                account.isActive,
                SalesTotal = _db.DirectSales
                    .Where(sale =>
                        sale.PersonalAccountId == account.Id &&
                        sale.PaymentMethod == PaymentMethod.CuentaPersonal &&
                        !sale.IsVoided)
                    .Sum(sale => (decimal?) sale.Total) ?? 0m,
                PaymentsTotal = _db.PersonalAccountPayments
                    .Where(payment => payment.PersonalAccountId == account.Id)
                    .Sum(payment => (decimal?) payment.Amount) ?? 0m,
                OldestSaleDate = _db.DirectSales
                    .Where(sale =>
                        sale.PersonalAccountId == account.Id &&
                        sale.PaymentMethod == PaymentMethod.CuentaPersonal &&
                        !sale.IsVoided)
                    .Min(sale => (DateTime?) sale.Date)
            });

        var query = totalsQuery.Select(account => new
        {
            account.Id,
            account.FullName,
            account.DNI,
            account.Phone,
            account.Address,
            account.isActive,
            DebtValue = account.SalesTotal > account.PaymentsTotal
                ? account.SalesTotal - account.PaymentsTotal
                : 0m,
            DebtSinceValue = account.SalesTotal > account.PaymentsTotal
                ? account.OldestSaleDate
                : null
        });

        var recordsTotal = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = DataTableSearchTerm.Create(request.Search);
            var date = search.Date;
            var matchesActive = search.EqualsLabel("Activa") ||
                (!search.EqualsLabel("Inactiva") && search.MatchesLabel("Activa"));
            var matchesInactive = search.EqualsLabel("Inactiva") ||
                (!search.EqualsLabel("Activa") && search.MatchesLabel("Inactiva"));

            query = query.Where(account =>
                EF.Functions.ILike(account.FullName, search.Pattern) ||
                EF.Functions.ILike(account.DNI, search.Pattern) ||
                (account.Phone != null && EF.Functions.ILike(account.Phone, search.Pattern)) ||
                (account.Address != null && EF.Functions.ILike(account.Address, search.Pattern)) ||
                (search.HasNumber && account.DebtValue == search.Number) ||
                (date != null && account.DebtSinceValue.HasValue &&
                    account.DebtSinceValue.Value.Day == date.Day &&
                    account.DebtSinceValue.Value.Month == date.Month &&
                    (!date.Year.HasValue || account.DebtSinceValue.Value.Year == date.Year.Value)) ||
                (matchesActive && account.isActive) ||
                (matchesInactive && !account.isActive));
        }

        var recordsFiltered = await query.CountAsync();

        query = request.OrderColumn switch
        {
            0 => request.IsAscending
                ? query.OrderBy(account => account.FullName)
                : query.OrderByDescending(account => account.FullName),
            1 => request.IsAscending
                ? query.OrderBy(account => account.DNI)
                : query.OrderByDescending(account => account.DNI),
            2 => request.IsAscending
                ? query.OrderBy(account => account.Phone)
                : query.OrderByDescending(account => account.Phone),
            3 => request.IsAscending
                ? query.OrderBy(account => account.DebtValue)
                : query.OrderByDescending(account => account.DebtValue),
            4 => request.IsAscending
                ? query.OrderBy(account => account.DebtSinceValue)
                : query.OrderByDescending(account => account.DebtSinceValue),
            5 => request.IsAscending
                ? query.OrderBy(account => account.isActive)
                : query.OrderByDescending(account => account.isActive),
            _ => query.OrderBy(account => account.FullName)
        };

        var accounts = await query
            .Skip(request.Start)
            .Take(request.Length)
            .ToListAsync();

        var debtSummaries = await GetDebtSummariesAsync(accounts.Select(account => account.Id));
        var data = accounts.Select(account =>
        {
            var debt = debtSummaries.GetValueOrDefault(account.Id, DebtSummary.Empty);

            return new
            {
                account.Id,
                account.FullName,
                account.DNI,
                account.Phone,
                account.isActive,
                debt = debt.Amount,
                debtSince = debt.Since?.ToString("dd/MM/yyyy")
            };
        });

        return Json(new
        {
            draw = request.Draw,
            recordsTotal,
            recordsFiltered,
            data
        });
    }

    [HttpGet]
    public async Task<IActionResult> Upsert(int? id)
    {
        if (!id.HasValue)
        {
            return View(new PersonalAccount());
        }

        var account = await _db.PersonalAccounts.FindAsync(id.Value);
        return account is null ? NotFound() : View(account);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Upsert(PersonalAccount input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        PersonalAccount account;

        if (input.Id == 0)
        {
            account = new PersonalAccount();
            _db.PersonalAccounts.Add(account);
        }
        else
        {
            var existingAccount = await _db.PersonalAccounts.FindAsync(input.Id);

            if (existingAccount is null)
            {
                return NotFound();
            }

            account = existingAccount;
        }

        account.FullName = input.FullName.Trim();
        account.DNI = input.DNI.Trim();
        account.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
        account.Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim();
        account.isActive = input.isActive;

        await _db.SaveChangesAsync();
        TempData["success"] = "Cuenta personal guardada correctamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var account = await _db.PersonalAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (account is null)
        {
            return NotFound();
        }

        var summary = (await GetDebtSummariesAsync(new[] { id }))
            .GetValueOrDefault(id, DebtSummary.Empty);

        return View(new PersonalAccountDetailVM
        {
            Account = account,
            CurrentDebt = summary.Amount,
            DebtSince = summary.Since
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(PersonalAccountSettleVM input)
    {
        var accountExists = await _db.PersonalAccounts.AnyAsync(account => account.Id == input.Id);

        if (!accountExists)
        {
            return NotFound();
        }

        var summary = (await GetDebtSummariesAsync(new[] { input.Id }))
            .GetValueOrDefault(input.Id, DebtSummary.Empty);

        if (summary.Amount <= 0)
        {
            TempData["error"] = "La cuenta no tiene deuda pendiente";
            return RedirectToAction(nameof(Detail), new { id = input.Id });
        }

        var amount = summary.Amount;

        if (!string.IsNullOrWhiteSpace(input.AmountText) &&
            (!DecimalParser.TryParse(input.AmountText, out amount) || amount <= 0))
        {
            TempData["error"] = "Ingrese un monto válido";
            return RedirectToAction(nameof(Detail), new { id = input.Id });
        }

        if (amount > summary.Amount)
        {
            TempData["error"] = "El pago no puede superar la deuda pendiente";
            return RedirectToAction(nameof(Detail), new { id = input.Id });
        }

        if (input.PaymentMethod is not PaymentMethod.Efectivo and not PaymentMethod.Transferencia)
        {
            TempData["error"] = "Seleccione efectivo o transferencia";
            return RedirectToAction(nameof(Detail), new { id = input.Id });
        }

        _db.PersonalAccountPayments.Add(new PersonalAccountPayment
        {
            PersonalAccountId = input.Id,
            Amount = amount,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            Date = DateTime.Now,
            PaymentMethod = input.PaymentMethod,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        });

        await _db.SaveChangesAsync();
        TempData["success"] = "Pago registrado correctamente";
        return RedirectToAction(nameof(Detail), new { id = input.Id });
    }

    [HttpGet]
    public async Task<IActionResult> GetSaleDetails(int id)
    {
        var request = DataTableRequest.From(Request);
        var saleBalances = await GetSaleBalancesAsync(id);
        var query = _db.DirectSaleItems
            .AsNoTracking()
            .Where(item =>
                item.DirectSale.PersonalAccountId == id &&
                item.DirectSale.PaymentMethod == PaymentMethod.CuentaPersonal &&
                !item.DirectSale.IsVoided);

        var recordsTotal = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = DataTableSearchTerm.Create(request.Search);
            var date = search.Date;
            var matchingSaleIds = saleBalances
                .Where(item => search.MatchesLabel(GetSaleStatus(item.Value).Text))
                .Select(item => item.Key)
                .ToList();

            query = query.Where(item =>
                EF.Functions.ILike(item.ProductName, search.Pattern) ||
                (search.HasNumber && item.Quantity == search.Number) ||
                (search.HasNumber && item.UnitPrice == search.Number) ||
                (search.HasNumber && item.Subtotal == search.Number) ||
                (date != null &&
                    item.DirectSale.Date.Day == date.Day &&
                    item.DirectSale.Date.Month == date.Month &&
                    (!date.Year.HasValue || item.DirectSale.Date.Year == date.Year.Value) &&
                    (!date.Hour.HasValue || item.DirectSale.Date.Hour == date.Hour.Value) &&
                    (!date.Minute.HasValue || item.DirectSale.Date.Minute == date.Minute.Value)) ||
                matchingSaleIds.Contains(item.DirectSaleId));
        }

        var recordsFiltered = await query.CountAsync();

        query = request.OrderColumn switch
        {
            0 => request.IsAscending
                ? query.OrderBy(item => item.DirectSale.Date)
                : query.OrderByDescending(item => item.DirectSale.Date),
            1 => request.IsAscending
                ? query.OrderBy(item => item.ProductName)
                : query.OrderByDescending(item => item.ProductName),
            2 => request.IsAscending
                ? query.OrderBy(item => item.Quantity)
                : query.OrderByDescending(item => item.Quantity),
            3 => request.IsAscending
                ? query.OrderBy(item => item.UnitPrice)
                : query.OrderByDescending(item => item.UnitPrice),
            4 => request.IsAscending
                ? query.OrderBy(item => item.Subtotal)
                : query.OrderByDescending(item => item.Subtotal),
            _ => query.OrderByDescending(item => item.DirectSale.Date)
        };

        var rows = await query
            .Skip(request.Start)
            .Take(request.Length)
            .Select(item => new
            {
                item.DirectSaleId,
                item.DirectSale.Date,
                item.ProductName,
                item.Quantity,
                item.UnitPrice,
                item.Subtotal
            })
            .ToListAsync();

        var data = rows.Select(item =>
        {
            var balance = saleBalances.GetValueOrDefault(item.DirectSaleId);
            var status = GetSaleStatus(balance);

            return new
            {
                date = item.Date.ToString("dd/MM/yyyy HH:mm"),
                product = item.ProductName,
                quantity = FormatQuantity(item.Quantity),
                item.UnitPrice,
                item.Subtotal,
                status = status.Text,
                statusClass = status.ClassName
            };
        });

        return Json(new
        {
            draw = request.Draw,
            recordsTotal,
            recordsFiltered,
            data
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetPayments(int id)
    {
        var request = DataTableRequest.From(Request);
        var query = _db.PersonalAccountPayments
            .AsNoTracking()
            .Where(payment => payment.PersonalAccountId == id);
        var recordsTotal = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = DataTableSearchTerm.Create(request.Search);
            var date = search.Date;
            var matchesCash = search.MatchesLabel("Efectivo");
            var matchesTransfer = search.MatchesLabel("Transferencia");

            query = query.Where(payment =>
                (payment.Notes != null && EF.Functions.ILike(payment.Notes, search.Pattern)) ||
                (payment.User != null && EF.Functions.ILike(payment.User.FullName, search.Pattern)) ||
                (search.HasNumber && payment.Amount == search.Number) ||
                (date != null &&
                    payment.Date.Day == date.Day &&
                    payment.Date.Month == date.Month &&
                    (!date.Year.HasValue || payment.Date.Year == date.Year.Value) &&
                    (!date.Hour.HasValue || payment.Date.Hour == date.Hour.Value) &&
                    (!date.Minute.HasValue || payment.Date.Minute == date.Minute.Value)) ||
                (matchesCash && payment.PaymentMethod == PaymentMethod.Efectivo) ||
                (matchesTransfer && payment.PaymentMethod == PaymentMethod.Transferencia));
        }

        var recordsFiltered = await query.CountAsync();

        query = request.OrderColumn switch
        {
            0 => request.IsAscending
                ? query.OrderBy(payment => payment.Date)
                : query.OrderByDescending(payment => payment.Date),
            1 => request.IsAscending
                ? query.OrderBy(payment => payment.PaymentMethod)
                : query.OrderByDescending(payment => payment.PaymentMethod),
            2 => request.IsAscending
                ? query.OrderBy(payment => payment.Amount)
                : query.OrderByDescending(payment => payment.Amount),
            3 => request.IsAscending
                ? query.OrderBy(payment => payment.User!.FullName)
                : query.OrderByDescending(payment => payment.User!.FullName),
            4 => request.IsAscending
                ? query.OrderBy(payment => payment.Notes)
                : query.OrderByDescending(payment => payment.Notes),
            _ => query.OrderByDescending(payment => payment.Date)
        };

        var payments = await query
            .Skip(request.Start)
            .Take(request.Length)
            .Select(payment => new
            {
                payment.Date,
                payment.PaymentMethod,
                payment.Amount,
                User = payment.User != null ? payment.User.FullName : null,
                payment.Notes
            })
            .ToListAsync();

        var data = payments.Select(payment => new
        {
            date = payment.Date.ToString("dd/MM/yyyy HH:mm"),
            paymentMethod = payment.PaymentMethod == PaymentMethod.Transferencia
                ? "Transferencia"
                : "Efectivo",
            payment.Amount,
            user = string.IsNullOrWhiteSpace(payment.User) ? "Sin usuario" : payment.User,
            notes = string.IsNullOrWhiteSpace(payment.Notes) ? "-" : payment.Notes
        });

        return Json(new
        {
            draw = request.Draw,
            recordsTotal,
            recordsFiltered,
            data
        });
    }

    private async Task<Dictionary<int, DebtSummary>> GetDebtSummariesAsync(IEnumerable<int> accountIds)
    {
        var ids = accountIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<int, DebtSummary>();
        }

        var sales = await _db.DirectSales
            .AsNoTracking()
            .Where(sale =>
                sale.PersonalAccountId.HasValue &&
                ids.Contains(sale.PersonalAccountId.Value) &&
                sale.PaymentMethod == PaymentMethod.CuentaPersonal &&
                !sale.IsVoided)
            .OrderBy(sale => sale.Date)
            .ThenBy(sale => sale.Id)
            .Select(sale => new
            {
                AccountId = sale.PersonalAccountId!.Value,
                sale.Date,
                sale.Total
            })
            .ToListAsync();

        var payments = await _db.PersonalAccountPayments
            .AsNoTracking()
            .Where(payment => ids.Contains(payment.PersonalAccountId))
            .GroupBy(payment => payment.PersonalAccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                Total = group.Sum(payment => payment.Amount)
            })
            .ToDictionaryAsync(item => item.AccountId, item => item.Total);

        var result = ids.ToDictionary(id => id, _ => DebtSummary.Empty);

        foreach (var salesGroup in sales.GroupBy(sale => sale.AccountId))
        {
            var availablePayments = payments.GetValueOrDefault(salesGroup.Key);
            var debt = 0m;
            DateTime? debtSince = null;

            foreach (var sale in salesGroup)
            {
                var amountPaid = Math.Min(availablePayments, sale.Total);
                var remaining = sale.Total - amountPaid;
                availablePayments -= amountPaid;

                if (remaining <= 0)
                {
                    continue;
                }

                debt += remaining;
                debtSince ??= sale.Date;
            }

            result[salesGroup.Key] = new DebtSummary(debt, debtSince);
        }

        return result;
    }

    private async Task<Dictionary<int, SaleBalance>> GetSaleBalancesAsync(int accountId)
    {
        var sales = await _db.DirectSales
            .AsNoTracking()
            .Where(sale =>
                sale.PersonalAccountId == accountId &&
                sale.PaymentMethod == PaymentMethod.CuentaPersonal &&
                !sale.IsVoided)
            .OrderBy(sale => sale.Date)
            .ThenBy(sale => sale.Id)
            .Select(sale => new { sale.Id, sale.Total })
            .ToListAsync();
        var availablePayments = await _db.PersonalAccountPayments
            .AsNoTracking()
            .Where(payment => payment.PersonalAccountId == accountId)
            .SumAsync(payment => (decimal?) payment.Amount) ?? 0m;
        var result = new Dictionary<int, SaleBalance>();

        foreach (var sale in sales)
        {
            var amountPaid = Math.Min(availablePayments, sale.Total);
            result[sale.Id] = new SaleBalance(sale.Total, amountPaid);
            availablePayments -= amountPaid;
        }

        return result;
    }

    private static (string Text, string ClassName) GetSaleStatus(SaleBalance? balance)
    {
        if (balance is null || balance.AmountPaid <= 0)
        {
            return ("Pendiente", "status-inactive");
        }

        return balance.AmountPaid >= balance.Total
            ? ("Saldada", "status-active")
            : ("Pago parcial", "status-warning");
    }

    private static string FormatQuantity(decimal quantity)
    {
        return quantity % 1 == 0
            ? quantity.ToString("N0")
            : quantity.ToString("N2");
    }

    private sealed record DebtSummary(decimal Amount, DateTime? Since)
    {
        public static DebtSummary Empty { get; } = new(0m, null);
    }

    private sealed record SaleBalance(decimal Total, decimal AmountPaid);
}
