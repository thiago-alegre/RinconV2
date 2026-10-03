using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rincon.DataAccess.Data;
using Rincon.Infrastructure;
using Rincon.Models;
using Rincon.Utilities;
using Rincon.Utilities.Enums;

namespace Rincon.Areas.Employee.Controllers;

[Area("Employee")]
[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
public class ExpensesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ExpensesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public IActionResult Index(DateTime? dateFrom, DateTime? dateTo)
    {
        var (from, to) = BusinessInput.NormalizeDateRange(dateFrom, dateTo);
        ViewBag.DateFrom = from;
        ViewBag.DateTo = to;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(DateTime? dateFrom, DateTime? dateTo)
    {
        var request = DataTableRequest.From(Request);
        var (from, to) = BusinessInput.NormalizeDateRange(dateFrom, dateTo);
        var endExclusive = BusinessInput.ExclusiveEnd(to);

        var query = _db.Expenses
            .AsNoTracking()
            .Where(expense =>
                !expense.IsVoided &&
                expense.Date >= from &&
                expense.Date < endExclusive);

        var recordsTotal = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = DataTableSearchTerm.Create(request.Search);
            var date = search.Date;
            var matchesPurchase = search.MatchesLabel("Compra de mercadería");
            var matchesBusinessExpense = search.MatchesLabel("Gasto del negocio");
            var matchesWithdrawal = search.MatchesLabel("Retiro personal");
            var matchesCash = search.MatchesLabel("Efectivo");
            var matchesTransfer = search.MatchesLabel("Transferencia");

            query = query.Where(expense =>
                EF.Functions.ILike(expense.Concept, search.Pattern) ||
                (expense.Notes != null && EF.Functions.ILike(expense.Notes, search.Pattern)) ||
                (expense.User != null && EF.Functions.ILike(expense.User.FullName, search.Pattern)) ||
                (search.HasNumber && expense.Amount == search.Number) ||
                (date != null &&
                    expense.Date.Day == date.Day &&
                    expense.Date.Month == date.Month &&
                    (!date.Year.HasValue || expense.Date.Year == date.Year.Value) &&
                    (!date.Hour.HasValue || expense.Date.Hour == date.Hour.Value) &&
                    (!date.Minute.HasValue || expense.Date.Minute == date.Minute.Value)) ||
                (matchesPurchase && expense.Type == ExpenseType.CompraMercaderia) ||
                (matchesBusinessExpense && expense.Type == ExpenseType.GastoNegocio) ||
                (matchesWithdrawal && expense.Type == ExpenseType.RetiroPersonal) ||
                (matchesCash && expense.PaymentMethod == PaymentMethod.Efectivo) ||
                (matchesTransfer && expense.PaymentMethod == PaymentMethod.Transferencia));
        }

        var recordsFiltered = await query.CountAsync();
        var totalAmount = await query.SumAsync(expense => (decimal?) expense.Amount) ?? 0m;

        query = request.OrderColumn switch
        {
            0 => request.IsAscending
                ? query.OrderBy(expense => expense.Date)
                : query.OrderByDescending(expense => expense.Date),
            1 => request.IsAscending
                ? query.OrderBy(expense => expense.Type)
                : query.OrderByDescending(expense => expense.Type),
            2 => request.IsAscending
                ? query.OrderBy(expense => expense.Concept)
                : query.OrderByDescending(expense => expense.Concept),
            3 => request.IsAscending
                ? query.OrderBy(expense => expense.PaymentMethod)
                : query.OrderByDescending(expense => expense.PaymentMethod),
            4 => request.IsAscending
                ? query.OrderBy(expense => expense.Amount)
                : query.OrderByDescending(expense => expense.Amount),
            _ => query.OrderByDescending(expense => expense.Date).ThenByDescending(expense => expense.Id)
        };

        var expenses = await query
            .Skip(request.Start)
            .Take(request.Length)
            .Select(expense => new
            {
                expense.Id,
                expense.Date,
                expense.Type,
                expense.Concept,
                expense.Notes,
                expense.PaymentMethod,
                expense.Amount
            })
            .ToListAsync();

        var data = expenses.Select(expense => new
        {
            expense.Id,
            date = expense.Date.ToString("dd/MM/yyyy HH:mm"),
            type = GetExpenseTypeName(expense.Type),
            expense.Concept,
            expense.Notes,
            paymentMethod = DisplayFormatting.PaymentMethodName(expense.PaymentMethod),
            expense.Amount
        });

        return Json(new
        {
            draw = request.Draw,
            recordsTotal,
            recordsFiltered,
            totalAmount,
            data
        });
    }

    [HttpGet]
    public IActionResult Create() => View(new Expense { Date = DateTime.Now, OperationId = Guid.NewGuid() });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("OperationId,Date,Type,Concept,Amount,PaymentMethod,Notes")] Expense expense)
    {
        if (expense.OperationId is null || expense.OperationId == Guid.Empty) return BadRequest();
        var previous = await _db.Expenses.AsNoTracking().FirstOrDefaultAsync(e => e.OperationId == expense.OperationId);
        if (previous is not null)
            return previous.UserId == _userManager.GetUserId(User) ? RedirectToAction(nameof(Index)) : Conflict();
        ValidatePaymentMethod(expense);

        if (!ModelState.IsValid)
        {
            return View(expense);
        }

        expense.UserId = _userManager.GetUserId(User);
        expense.IsVoided = false;
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();
        TempData["success"] = "La salida de dinero se registró correctamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var expense = await _db.Expenses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsVoided);

        return expense is null
            ? NotFound()
            : View("Create", expense);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Id,Version,Date,Type,Concept,Amount,PaymentMethod,Notes")] Expense input)
    {
        if (id != input.Id)
        {
            return BadRequest();
        }

        ValidatePaymentMethod(input);

        if (!ModelState.IsValid)
        {
            return View("Create", input);
        }

        var expense = await _db.Expenses
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsVoided);

        if (expense is null)
        {
            return NotFound();
        }

        _db.Entry(expense).Property(e => e.Version).OriginalValue = input.Version;
        expense.Date = input.Date;
        expense.Type = input.Type;
        expense.Concept = input.Concept.Trim();
        expense.Amount = input.Amount;
        expense.PaymentMethod = input.PaymentMethod;
        expense.Notes = string.IsNullOrWhiteSpace(input.Notes)
            ? null
            : input.Notes.Trim();

        await _db.SaveChangesAsync();

        TempData["success"] = "La salida de dinero se actualizó correctamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(int id)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == id && !e.IsVoided);

        if (expense is null)
        {
            return Json(new { success = false, message = "Movimiento no encontrado" });
        }

        expense.IsVoided = true;
        expense.VoidedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "El movimiento fue anulado y permanece en el historial"
        });
    }

    private static string GetExpenseTypeName(ExpenseType type)
    {
        return type switch
        {
            ExpenseType.CompraMercaderia => "Compra de mercadería",
            ExpenseType.GastoNegocio => "Gasto del negocio",
            ExpenseType.RetiroPersonal => "Retiro personal",
            _ => "Sin especificar"
        };
    }

    private void ValidatePaymentMethod(Expense expense)
    {
        BusinessInput.Date(ModelState, expense.Date);
        BusinessInput.Money(ModelState, nameof(expense.Amount), expense.Amount, true);
        if (!Enum.IsDefined(expense.Type)) ModelState.AddModelError(nameof(expense.Type), "Seleccione un tipo válido.");
        if (expense.PaymentMethod is not PaymentMethod.Efectivo and not PaymentMethod.Transferencia)
        {
            ModelState.AddModelError(
                nameof(expense.PaymentMethod),
                "Seleccione efectivo o transferencia");
        }
    }
}
