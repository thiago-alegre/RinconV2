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
        var query = AccountLedger.Accounts(_db);
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
        if (input.Id == 0)
        {
            BusinessInput.Money(ModelState, nameof(input.OpeningBalance), input.OpeningBalance);
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        PersonalAccount account;

        if (input.Id == 0)
        {
            account = new PersonalAccount();
            account.OpeningBalance = input.OpeningBalance;
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
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (input.OperationId == Guid.Empty) return BadRequest();
        var previous = await _db.PersonalAccountPayments.AsNoTracking().FirstOrDefaultAsync(p => p.OperationId == input.OperationId);
        if (previous is not null)
            return previous.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier) && previous.PersonalAccountId == input.Id
                ? RedirectToAction(nameof(Detail), new { id = previous.PersonalAccountId }) : Conflict();
        if (!ModelState.IsValid || input.Notes?.Length > 500) return BadRequest();
        var accountExists = await _db.PersonalAccounts.AnyAsync(account => account.Id == input.Id && account.isActive);

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

        if (!BusinessInput.IsMoney(amount)) return BadRequest();

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
            OperationId = input.OperationId,
            PersonalAccountId = input.Id,
            Amount = amount,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            Date = DateTime.Now,
            PaymentMethod = input.PaymentMethod,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
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
                item.DirectSale.PaymentMethod == PaymentMethod.CuentaPersonal);

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
                item.Subtotal,
                ReturnedQuantity = item.ReturnItems.Sum(value => value.Quantity),
                ReturnedTotal = item.ReturnItems.Sum(value => value.Subtotal)
            })
            .ToListAsync();

        var data = rows.Select(item =>
        {
            var balance = saleBalances.GetValueOrDefault(item.DirectSaleId);
            var status = item.ReturnedQuantity >= item.Quantity
                ? (Text: "Anulada", ClassName: "status-inactive")
                : item.ReturnedQuantity > 0
                    ? (Text: "Anulación parcial", ClassName: "status-warning")
                    : GetSaleStatus(balance);

            return new
            {
                date = item.Date.ToString("dd/MM/yyyy HH:mm"),
                product = item.ProductName,
                quantity = item.ReturnedQuantity > 0
                    ? $"{DisplayFormatting.Quantity(item.Quantity)} · anulada {DisplayFormatting.Quantity(item.ReturnedQuantity)}"
                    : DisplayFormatting.Quantity(item.Quantity),
                item.UnitPrice,
                subtotal = item.Subtotal - item.ReturnedTotal,
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
    public async Task<IActionResult> GetCancellations(int id)
    {
        var request = DataTableRequest.From(Request);
        var query = _db.DirectSaleReturns
            .AsNoTracking()
            .Where(item => item.DirectSale.PersonalAccountId == id);
        var recordsTotal = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = DataTableSearchTerm.Create(request.Search);
            query = query.Where(item =>
                (item.Reason != null && EF.Functions.ILike(item.Reason, search.Pattern)) ||
                item.Items.Any(detail => EF.Functions.ILike(detail.ProductName, search.Pattern)) ||
                (search.HasNumber && (item.Total == search.Number || item.DirectSaleId == search.Number)));
        }

        var recordsFiltered = await query.CountAsync();
        var rows = await query
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Id)
            .Skip(request.Start)
            .Take(request.Length)
            .Select(item => new
            {
                item.Date,
                item.DirectSaleId,
                Products = item.Items.Select(detail => new { detail.ProductName, detail.Quantity }).ToList(),
                item.RefundMethod,
                item.Total,
                User = item.User != null ? item.User.FullName : null,
                item.Reason
            })
            .ToListAsync();

        return Json(new
        {
            draw = request.Draw,
            recordsTotal,
            recordsFiltered,
            data = rows.Select(item => new
            {
                date = item.Date.ToString("dd/MM/yyyy HH:mm"),
                sale = $"#{item.DirectSaleId}",
                products = string.Join(", ", item.Products.Select(detail =>
                    $"{detail.ProductName} x{DisplayFormatting.Quantity(detail.Quantity)}")),
                refundMethod = item.RefundMethod switch
                {
                    PaymentMethod.Efectivo => "Efectivo",
                    PaymentMethod.Transferencia => "Transferencia",
                    PaymentMethod.CuentaPersonal => "Ajuste de cuenta",
                    _ => "-"
                },
                item.Total,
                user = item.User ?? "Sin usuario",
                reason = item.Reason ?? "-"
            })
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
        return await AccountLedger.Accounts(_db).Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new DebtSummary(a.DebtValue, a.DebtSinceValue));
    }

    private async Task<Dictionary<int, SaleBalance>> GetSaleBalancesAsync(int accountId) =>
        await AccountLedger.Sales(_db).Where(s => s.AccountId == accountId)
            .ToDictionaryAsync(s => s.Id, s => new SaleBalance(s.Total, s.AmountPaid));

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

    private sealed record DebtSummary(decimal Amount, DateTime? Since)
    {
        public static DebtSummary Empty { get; } = new(0m, null);
    }

    private sealed record SaleBalance(decimal Total, decimal AmountPaid);
}
