using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rincon.DataAccess.Data;
using Rincon.Infrastructure;
using Rincon.Models;
using Rincon.Models.ViewModels;
using Rincon.Utilities;
using Rincon.Utilities.Enums;

namespace Rincon.Areas.Employee.Controllers;

[Area("Employee"), Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
public class SalesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public SalesController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public IActionResult Index(DateTime? dateFrom, DateTime? dateTo, string? status)
    {
        var (from, to) = NormalizeDateRange(dateFrom, dateTo);
        ViewBag.DateFrom = from;
        ViewBag.DateTo = to;
        ViewBag.Status = NormalizeStatus(status);

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        DateTime? dateFrom,
        DateTime? dateTo,
        string? status)
    {
        var request = DataTableRequest.From(Request);
        var (from, to) = NormalizeDateRange(dateFrom, dateTo);
        var endExclusive = to.AddDays(1);
        var normalizedStatus = NormalizeStatus(status);

        var query = _db.DirectSales
            .AsNoTracking()
            .Include(sale => sale.User)
            .Include(sale => sale.PersonalAccount)
            .Include(sale => sale.Items)
            .Where(sale => sale.Date >= from && sale.Date < endExclusive);

        query = normalizedStatus switch
        {
            "active" => query.Where(sale => !sale.IsVoided),
            "voided" => query.Where(sale => sale.IsVoided),
            _ => query
        };

        var recordsTotal = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = DataTableSearchTerm.Create(request.Search);
            var date = search.Date;
            var matchesCash = search.MatchesLabel("Efectivo");
            var matchesTransfer = search.MatchesLabel("Transferencia");
            var matchesAccount = search.MatchesLabel("Cuenta personal");
            var matchesActive = search.MatchesLabel("Vigente");
            var matchesVoided = search.MatchesLabel("Anulada");

            query = query.Where(sale =>
                (sale.User != null &&
                    (EF.Functions.ILike(sale.User.FullName, search.Pattern) ||
                     (sale.User.Email != null && EF.Functions.ILike(sale.User.Email, search.Pattern)))) ||
                (sale.PersonalAccount != null &&
                    (EF.Functions.ILike(sale.PersonalAccount.FullName, search.Pattern) ||
                     EF.Functions.ILike(sale.PersonalAccount.DNI, search.Pattern))) ||
                sale.Items.Any(item =>
                    EF.Functions.ILike(item.ProductName, search.Pattern) ||
                    (search.HasNumber &&
                        (item.Quantity == search.Number ||
                         item.UnitPrice == search.Number ||
                         item.UnitCost == search.Number ||
                         item.Subtotal == search.Number))) ||
                (search.HasNumber &&
                    (sale.Id == search.Number ||
                     sale.Total == search.Number ||
                     sale.TotalCost == search.Number ||
                     sale.Total - sale.TotalCost == search.Number)) ||
                (date != null &&
                    sale.Date.Day == date.Day &&
                    sale.Date.Month == date.Month &&
                    (!date.Year.HasValue || sale.Date.Year == date.Year.Value) &&
                    (!date.Hour.HasValue || sale.Date.Hour == date.Hour.Value) &&
                    (!date.Minute.HasValue || sale.Date.Minute == date.Minute.Value)) ||
                (matchesCash && sale.PaymentMethod == PaymentMethod.Efectivo) ||
                (matchesTransfer && sale.PaymentMethod == PaymentMethod.Transferencia) ||
                (matchesAccount && sale.PaymentMethod == PaymentMethod.CuentaPersonal) ||
                (matchesActive && !sale.IsVoided) ||
                (matchesVoided && sale.IsVoided));
        }

        var recordsFiltered = await query.CountAsync();

        query = request.OrderColumn switch
        {
            0 => request.IsAscending
                ? query.OrderBy(sale => sale.Date)
                : query.OrderByDescending(sale => sale.Date),
            1 => request.IsAscending
                ? query.OrderBy(sale => sale.Id)
                : query.OrderByDescending(sale => sale.Id),
            3 => request.IsAscending
                ? query.OrderBy(sale => sale.PaymentMethod)
                : query.OrderByDescending(sale => sale.PaymentMethod),
            4 => request.IsAscending
                ? query.OrderBy(sale => sale.Total)
                : query.OrderByDescending(sale => sale.Total),
            5 => request.IsAscending
                ? query.OrderBy(sale => sale.Total - sale.TotalCost)
                : query.OrderByDescending(sale => sale.Total - sale.TotalCost),
            6 => request.IsAscending
                ? query.OrderBy(sale => sale.User != null ? sale.User.FullName : string.Empty)
                : query.OrderByDescending(sale => sale.User != null ? sale.User.FullName : string.Empty),
            7 => request.IsAscending
                ? query.OrderBy(sale => sale.IsVoided)
                : query.OrderByDescending(sale => sale.IsVoided),
            _ => query.OrderByDescending(sale => sale.Date).ThenByDescending(sale => sale.Id)
        };

        var sales = await query
            .Skip(request.Start)
            .Take(request.Length)
            .ToListAsync();

        var data = sales.Select(sale => new
        {
            sale.Id,
            date = sale.Date.ToString("dd/MM/yyyy HH:mm"),
            products = string.Join(", ", sale.Items
                .OrderBy(item => item.Id)
                .Select(item => $"{item.ProductName} x{FormatQuantity(item.Quantity)}")),
            paymentMethod = GetPaymentMethodName(sale.PaymentMethod),
            account = sale.PaymentMethod == PaymentMethod.CuentaPersonal
                ? sale.PersonalAccount?.FullName ?? "Cuenta no disponible"
                : "-",
            sale.Total,
            profit = sale.Total - sale.TotalCost,
            user = string.IsNullOrWhiteSpace(sale.User?.FullName)
                ? sale.User?.Email ?? "Sin usuario"
                : sale.User.FullName,
            sale.IsVoided,
            detailUrl = Url.Action(nameof(Detail), new { id = sale.Id })
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
    public async Task<IActionResult> Detail(int id)
    {
        var sale = await _db.DirectSales
            .AsNoTracking()
            .Include(item => item.Items)
            .Include(item => item.User)
            .Include(item => item.PersonalAccount)
            .Include(item => item.VoidedByUser)
            .FirstOrDefaultAsync(item => item.Id == id);

        return sale is null ? NotFound() : View(sale);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(int id, string? reason)
    {
        var sale = await _db.DirectSales.FirstOrDefaultAsync(item => item.Id == id);

        if (sale is null)
        {
            return NotFound();
        }

        if (sale.IsVoided)
        {
            TempData["error"] = "La venta ya se encuentra anulada";
            return RedirectToAction(nameof(Detail), new { id });
        }

        if (sale.PaymentMethod == PaymentMethod.CuentaPersonal &&
            await GetAppliedPaymentAsync(sale) > 0)
        {
            TempData["error"] =
                "La venta tiene cobros de cuenta aplicados. Registre primero la devolución correspondiente antes de anularla.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var normalizedReason = reason?.Trim();

        if (normalizedReason?.Length > 500)
        {
            TempData["error"] = "El motivo no puede superar los 500 caracteres";
            return RedirectToAction(nameof(Detail), new { id });
        }

        sale.IsVoided = true;
        sale.VoidedAt = DateTime.Now;
        sale.VoidedByUserId = _users.GetUserId(User);
        sale.VoidReason = string.IsNullOrWhiteSpace(normalizedReason)
            ? "Anulación solicitada por el usuario"
            : normalizedReason;

        await _db.SaveChangesAsync();

        TempData["success"] = "La venta fue anulada y dejó de impactar en el balance";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var viewModel = new DirectSaleCreateVM();
        await LoadAsync(viewModel);

        return View(viewModel);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DirectSaleCreateVM vm)
    {
        vm.Lines = vm.Lines
            .Where(line => line.ProductId > 0 || line.Quantity != 1)
            .ToList();

        if (!vm.Lines.Any())
        {
            ModelState.AddModelError(string.Empty, "Agregue al menos un producto");
        }

        if (vm.PaymentMethod == PaymentMethod.CuentaPersonal && !vm.PersonalAccountId.HasValue)
        {
            ModelState.AddModelError(
                nameof(vm.PersonalAccountId),
                "Seleccione una cuenta personal");
        }

        if (vm.PaymentMethod is PaymentMethod.Combinado)
        {
            ModelState.AddModelError(
                nameof(vm.PaymentMethod),
                "Seleccione efectivo, transferencia o cuenta personal");
        }

        var productIds = vm.Lines
            .Select(line => line.ProductId)
            .Distinct()
            .ToList();
        var products = await _db.Products
            .Where(product => productIds.Contains(product.Id) && product.IsActive)
            .ToDictionaryAsync(product => product.Id);

        foreach (var line in vm.Lines)
        {
            if (!products.ContainsKey(line.ProductId) || line.Quantity <= 0)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Revise los productos y cantidades ingresados");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(vm);
            return View(vm);
        }

        var sale = new DirectSale
        {
            Date = vm.Date,
            PaymentMethod = vm.PaymentMethod,
            PersonalAccountId = vm.PaymentMethod == PaymentMethod.CuentaPersonal
                ? vm.PersonalAccountId
                : null,
            UserId = _users.GetUserId(User)
        };

        foreach (var line in vm.Lines)
        {
            var product = products[line.ProductId];
            var item = new DirectSaleItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = line.Quantity,
                UnitPrice = product.SalePrice,
                UnitCost = product.PurchasePrice,
                Subtotal = product.SalePrice * line.Quantity
            };

            sale.Items.Add(item);
            sale.Total += item.Subtotal;
            sale.TotalCost += item.UnitCost * item.Quantity;
        }

        _db.DirectSales.Add(sale);
        await _db.SaveChangesAsync();

        TempData["success"] = "Venta registrada correctamente";
        return RedirectToAction(nameof(Detail), new { id = sale.Id });
    }

    private async Task<decimal> GetAppliedPaymentAsync(DirectSale sale)
    {
        if (!sale.PersonalAccountId.HasValue)
        {
            return 0;
        }

        var availablePayments = await _db.PersonalAccountPayments
            .AsNoTracking()
            .Where(payment => payment.PersonalAccountId == sale.PersonalAccountId.Value)
            .SumAsync(payment => (decimal?)payment.Amount) ?? 0m;

        var accountSales = await _db.DirectSales
            .AsNoTracking()
            .Where(item =>
                item.PersonalAccountId == sale.PersonalAccountId &&
                item.PaymentMethod == PaymentMethod.CuentaPersonal &&
                !item.IsVoided)
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.Total })
            .ToListAsync();

        foreach (var accountSale in accountSales)
        {
            var appliedPayment = Math.Min(availablePayments, accountSale.Total);

            if (accountSale.Id == sale.Id)
            {
                return appliedPayment;
            }

            availablePayments -= appliedPayment;
        }

        return 0;
    }

    private static (DateTime From, DateTime To) NormalizeDateRange(
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        var from = (dateFrom ?? DateTime.Today.AddMonths(-1)).Date;
        var to = (dateTo ?? DateTime.Today).Date;

        return to < from ? (to, from) : (from, to);
    }

    private static string NormalizeStatus(string? status)
    {
        return status is "active" or "voided" ? status : "all";
    }

    private static string GetPaymentMethodName(PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Efectivo => "Efectivo",
            PaymentMethod.Transferencia => "Transferencia",
            PaymentMethod.CuentaPersonal => "Cuenta personal",
            _ => "Sin especificar"
        };
    }

    private static string FormatQuantity(decimal quantity)
    {
        return quantity % 1 == 0
            ? quantity.ToString("N0")
            : quantity.ToString("N2");
    }

    private async Task LoadAsync(DirectSaleCreateVM vm)
    {
        vm.Products = await _db.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .OrderBy(product => product.Name)
            .ToListAsync();
        vm.Accounts = await _db.PersonalAccounts
            .AsNoTracking()
            .Where(account => account.isActive)
            .OrderBy(account => account.FullName)
            .ToListAsync();

        if (!vm.Lines.Any())
        {
            vm.Lines.Add(new DirectSaleLineVM());
        }
    }
}
