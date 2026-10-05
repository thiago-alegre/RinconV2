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
using System.Data;

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
        var (from, to) = BusinessInput.NormalizeDateRange(dateFrom, dateTo);
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
        var (from, to) = BusinessInput.NormalizeDateRange(dateFrom, dateTo);
        var endExclusive = BusinessInput.ExclusiveEnd(to);
        var normalizedStatus = NormalizeStatus(status);

        var query = _db.DirectSales
            .AsNoTracking()
            .Include(sale => sale.User)
            .Include(sale => sale.PersonalAccount)
            .Include(sale => sale.Items)
            .Include(sale => sale.Returns)
            .Where(sale => sale.Date >= from && sale.Date < endExclusive);

        query = normalizedStatus switch
        {
            "active" => query.Where(sale => sale.Returns.Sum(item => item.Total) < sale.Total),
            "voided" => query.Where(sale => sale.Returns.Sum(item => item.Total) >= sale.Total),
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
            var matchesCombined = search.MatchesLabel("Combinado");
            var matchesActive = search.MatchesLabel("Vigente");
            var matchesVoided = search.MatchesLabel("Anulada");
            var matchesPartialCancellation = search.MatchesLabel("Anulación parcial");

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
                     sale.CashAmount == search.Number ||
                     sale.TransferAmount == search.Number ||
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
                (matchesCombined && sale.PaymentMethod == PaymentMethod.Combinado) ||
                (matchesActive && sale.Returns.Sum(item => item.Total) == 0) ||
                (matchesPartialCancellation && sale.Returns.Sum(item => item.Total) > 0 && sale.Returns.Sum(item => item.Total) < sale.Total) ||
                (matchesVoided && sale.Returns.Sum(item => item.Total) >= sale.Total));
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
                ? query.OrderBy(sale => sale.Returns.Sum(item => item.Total))
                : query.OrderByDescending(sale => sale.Returns.Sum(item => item.Total)),
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
                .Select(item => $"{item.ProductName} x{DisplayFormatting.Quantity(item.Quantity)}")),
            paymentMethod = DisplayFormatting.PaymentMethodName(sale.PaymentMethod),
            paymentBreakdown = sale.PaymentMethod == PaymentMethod.Combinado
                ? $"Efectivo {sale.CashAmount:C} · Transferencia {sale.TransferAmount:C}"
                : null,
            account = sale.PaymentMethod == PaymentMethod.CuentaPersonal
                ? sale.PersonalAccount?.FullName ?? "Cuenta no disponible"
                : "-",
            total = sale.Total - sale.Returns.Sum(item => item.Total),
            profit = (sale.Total - sale.TotalCost) - sale.Returns.Sum(item => item.Total - item.TotalCost),
            user = string.IsNullOrWhiteSpace(sale.User?.FullName)
                ? sale.User?.Email ?? "Sin usuario"
                : sale.User.FullName,
            returnStatus = GetCancellationStatus(sale),
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
            .Include(item => item.Returns)
                .ThenInclude(item => item.Items)
            .Include(item => item.Returns)
                .ThenInclude(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == id);

        return sale is null ? NotFound() : View(sale);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var viewModel = new DirectSaleCreateVM { OperationId = Guid.NewGuid() };
        await LoadAsync(viewModel);

        return View(viewModel);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DirectSaleCreateVM vm)
    {
        if (vm.OperationId == Guid.Empty)
            return BadRequest(); // Old forms must be reopened; never silently assign a retry a new identity.
        var previous = await _db.DirectSales.AsNoTracking()
            .FirstOrDefaultAsync(sale => sale.OperationId == vm.OperationId);
        if (previous is not null)
            return previous.UserId == _users.GetUserId(User)
                ? RedirectToAction(nameof(Detail), new { id = previous.Id }) : Conflict();
        BusinessInput.Date(ModelState, vm.Date);
        BusinessInput.Money(ModelState, nameof(vm.CashAmount), vm.CashAmount);
        BusinessInput.Money(ModelState, nameof(vm.TransferAmount), vm.TransferAmount);
        vm.Lines = vm.Lines
            .Where(line => line.IsLoose || line.ProductId.HasValue || line.Quantity != 1 ||
                !string.IsNullOrWhiteSpace(line.LooseName) || line.LooseUnitPrice != 0)
            .ToList();

        if (vm.Lines.Count > 100)
            return BadRequest();

        if (!vm.Lines.Any())
        {
            ModelState.AddModelError(string.Empty, "Agregue al menos un producto");
        }

        if (vm.PaymentMethod == PaymentMethod.CuentaPersonal &&
            !vm.PersonalAccountId.HasValue)
        {
            ModelState.AddModelError(
                nameof(vm.PersonalAccountId),
                "Seleccione una cuenta personal");
        }

        if (vm.PaymentMethod is not PaymentMethod.Efectivo and
                not PaymentMethod.Transferencia and
                not PaymentMethod.CuentaPersonal and
                not PaymentMethod.Combinado)
        {
            ModelState.AddModelError(
                nameof(vm.PaymentMethod),
                "Seleccione una forma de pago válida");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var productIds = vm.Lines
            .Where(line => !line.IsLoose && line.ProductId.HasValue)
            .Select(line => line.ProductId!.Value)
            .Distinct()
            .ToList();
        var products = await _db.Products
            .Where(product => productIds.Contains(product.Id) && product.IsActive)
            .ToDictionaryAsync(product => product.Id);

        foreach (var group in vm.Lines
            .Where(line => !line.IsLoose && line.ProductId.HasValue)
            .GroupBy(line => line.ProductId!.Value))
        {
            if (products.TryGetValue(group.Key, out var product) && group.Sum(line => (long)line.Quantity) > product.Quantity)
            {
                ModelState.AddModelError(string.Empty, $"Stock insuficiente para {product.Name}. Disponible: {product.Quantity}");
            }
        }

        foreach (var line in vm.Lines)
        {
            if (line.Quantity <= 0)
            {
                ModelState.AddModelError(string.Empty, "Revise las cantidades ingresadas");
                continue;
            }

            if (line.IsLoose)
            {
                line.LooseName = line.LooseName?.Trim();
                if (string.IsNullOrWhiteSpace(line.LooseName))
                    ModelState.AddModelError(string.Empty, "Ingresá una descripción para cada producto suelto");
                if (line.LooseUnitPrice <= 0)
                    ModelState.AddModelError(string.Empty, "Ingresá un precio mayor a cero para cada producto suelto");
                BusinessInput.Money(ModelState, "Precio del producto suelto", line.LooseUnitPrice, true);
            }
            else if (!line.ProductId.HasValue || !products.ContainsKey(line.ProductId.Value))
            {
                ModelState.AddModelError(string.Empty, "Revise los productos ingresados");
            }
        }

        var total = vm.Lines.Where(line => line.Quantity > 0).Sum(line =>
            line.IsLoose
                ? line.LooseUnitPrice * line.Quantity
                : line.ProductId.HasValue && products.TryGetValue(line.ProductId.Value, out var product)
                    ? product.SalePrice * line.Quantity
                    : 0);

        BusinessInput.Money(ModelState, "Total", total, true);

        if (vm.PaymentMethod == PaymentMethod.Combinado)
        {
            ValidateCombinedPayment(vm, total);
        }
        else if (vm.PaymentMethod == PaymentMethod.CuentaPersonal &&
                 vm.PersonalAccountId.HasValue)
        {
            var accountExists = await _db.PersonalAccounts
                .AsNoTracking()
                .AnyAsync(account =>
                    account.Id == vm.PersonalAccountId.Value &&
                    account.isActive);

            if (!accountExists)
            {
                ModelState.AddModelError(
                    nameof(vm.PersonalAccountId),
                    "La cuenta personal seleccionada no está disponible");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(vm);
            return View(vm);
        }

        var sale = new DirectSale
        {
            OperationId = vm.OperationId,
            Date = vm.Date,
            PaymentMethod = vm.PaymentMethod,
            PersonalAccountId = vm.PaymentMethod == PaymentMethod.CuentaPersonal
                ? vm.PersonalAccountId
                : null,
            CashAmount = vm.PaymentMethod == PaymentMethod.Combinado
                ? vm.CashAmount
                : vm.PaymentMethod == PaymentMethod.Efectivo ? total : 0,
            TransferAmount = vm.PaymentMethod == PaymentMethod.Combinado
                ? vm.TransferAmount
                : vm.PaymentMethod == PaymentMethod.Transferencia ? total : 0,
            UserId = _users.GetUserId(User)
        };

        foreach (var line in vm.Lines)
        {
            if (line.IsLoose)
            {
                var looseItem = new DirectSaleItem
                {
                    ProductId = null,
                    ProductName = line.LooseName!,
                    Quantity = line.Quantity,
                    UnitPrice = line.LooseUnitPrice,
                    UnitCost = 0,
                    Subtotal = line.LooseUnitPrice * line.Quantity
                };
                sale.Items.Add(looseItem);
                sale.Total += looseItem.Subtotal;
                continue;
            }

            var product = products[line.ProductId!.Value];
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
            product.Quantity -= line.Quantity;
        }

        _db.DirectSales.Add(sale);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["success"] = "Venta registrada correctamente";
        return RedirectToAction(nameof(Detail), new { id = sale.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Void(int id)
    {
        var sale = await _db.DirectSales
            .AsNoTracking()
            .Include(item => item.Items)
                .ThenInclude(item => item.ReturnItems)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (sale is null) return NotFound();
        if (sale.Returns.Sum(item => item.Total) >= sale.Total)
        {
            TempData["error"] = "La venta ya se encuentra anulada";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var vm = new DirectSaleVoidVM
        {
            IsAccountSale = sale.PaymentMethod == PaymentMethod.CuentaPersonal,
            OperationId = Guid.NewGuid(),
            SaleId = sale.Id,
            RefundMethod = sale.PaymentMethod,
            Lines = sale.Items.OrderBy(item => item.Id).Select(item => new DirectSaleVoidLineVM
            {
                DirectSaleItemId = item.Id,
                ProductName = item.ProductName,
                SoldQuantity = item.Quantity,
                ReturnedQuantity = item.ReturnItems.Sum(returnItem => returnItem.Quantity),
                UnitPrice = item.UnitPrice,
                IsLoose = !item.ProductId.HasValue,
                ReturnsToStock = item.ProductId.HasValue
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(DirectSaleVoidVM vm)
    {
        if (vm.OperationId == Guid.Empty) return BadRequest();
        var previous = await _db.DirectSaleReturns.AsNoTracking().FirstOrDefaultAsync(r => r.OperationId == vm.OperationId);
        if (previous is not null)
            return previous.UserId == _users.GetUserId(User) && previous.DirectSaleId == vm.SaleId
                ? RedirectToAction(nameof(Detail), new { id = previous.DirectSaleId }) : Conflict();
        BusinessInput.Date(ModelState, vm.Date);
        if (vm.Lines.Any(l => l.Quantity < 0 || l.Quantity % 1 != 0 || l.Quantity > int.MaxValue))
            return BadRequest();
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var sale = await _db.DirectSales
            .Include(item => item.Items)
                .ThenInclude(item => item.ReturnItems)
            .FirstOrDefaultAsync(item => item.Id == vm.SaleId);

        if (sale is null) return NotFound();
        if (sale.Returns.Sum(item => item.Total) >= sale.Total)
        {
            TempData["error"] = "La venta ya se encuentra anulada";
            return RedirectToAction(nameof(Detail), new { id = vm.SaleId });
        }

        vm.IsAccountSale = sale.PaymentMethod == PaymentMethod.CuentaPersonal;
        vm.RefundMethod = sale.PaymentMethod;

        var requested = vm.Lines
            .Where(item => item.Selected && item.Quantity > 0)
            .GroupBy(item => item.DirectSaleItemId)
            .Select(group => new DirectSaleVoidLineVM
            {
                DirectSaleItemId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                ReturnsToStock = group.Last().ReturnsToStock
            })
            .ToList();
        if (requested.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Seleccioná al menos un producto para anular");
        }

        foreach (var line in requested)
        {
            var item = sale.Items.FirstOrDefault(value => value.Id == line.DirectSaleItemId);
            var available = item is null ? 0 : item.Quantity - item.ReturnItems.Sum(value => value.Quantity);
            if (item is null || line.Quantity % 1 != 0 || line.Quantity <= 0 || line.Quantity > available)
            {
                ModelState.AddModelError(string.Empty, "Revisá las cantidades seleccionadas; no pueden superar lo pendiente de anular");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateVoidLinesAsync(vm, sale);
            return View(vm);
        }

        var isFullCancellation = sale.Items.All(item =>
            requested.Where(line => line.DirectSaleItemId == item.Id).Sum(line => line.Quantity) ==
            item.Quantity - item.ReturnItems.Sum(value => value.Quantity));
        var saleReturn = new DirectSaleReturn
        {
            OperationId = vm.OperationId,
            DirectSaleId = sale.Id,
            Date = vm.Date,
            RefundMethod = vm.RefundMethod,
            Reason = string.IsNullOrWhiteSpace(vm.Reason) ? null : vm.Reason.Trim(),
            UserId = _users.GetUserId(User)
        };
        var productIds = requested.Where(item => item.ReturnsToStock)
            .Select(item => sale.Items.First(value => value.Id == item.DirectSaleItemId).ProductId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct().ToList();
        var products = await _db.Products.Where(item => productIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id);

        foreach (var line in requested)
        {
            var item = sale.Items.First(value => value.Id == line.DirectSaleItemId);
            var returnItem = new DirectSaleReturnItem
            {
                DirectSaleItemId = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = line.Quantity,
                UnitPrice = item.UnitPrice,
                UnitCost = item.UnitCost,
                Subtotal = item.UnitPrice * line.Quantity,
                ReturnsToStock = line.ReturnsToStock && item.ProductId.HasValue
            };
            saleReturn.Items.Add(returnItem);
            saleReturn.Total += returnItem.Subtotal;
            saleReturn.TotalCost += returnItem.UnitCost * returnItem.Quantity;
            if (returnItem.ReturnsToStock && item.ProductId.HasValue &&
                products.TryGetValue(item.ProductId.Value, out var product))
            {
                product.Quantity = checked(product.Quantity + Decimal.ToInt32(line.Quantity));
            }
        }

        _db.DirectSaleReturns.Add(saleReturn);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["success"] = isFullCancellation
            ? "Venta anulada completamente"
            : "Anulación parcial registrada correctamente";
        return RedirectToAction(nameof(Detail), new { id = sale.Id });
    }

    private static string GetCancellationStatus(DirectSale sale)
    {
        var returned = sale.Returns.Sum(item => item.Total);
        if (returned <= 0) return "Vigente";
        return returned >= sale.Total ? "Anulada" : "Anulación parcial";
    }

    private static Task PopulateVoidLinesAsync(DirectSaleVoidVM vm, DirectSale sale)
    {
        var requested = vm.Lines
            .GroupBy(item => item.DirectSaleItemId)
            .ToDictionary(item => item.Key, item => item.Last());
        vm.Lines = sale.Items.OrderBy(item => item.Id).Select(item => new DirectSaleVoidLineVM
        {
            DirectSaleItemId = item.Id,
            ProductName = item.ProductName,
            SoldQuantity = item.Quantity,
            ReturnedQuantity = item.ReturnItems.Sum(value => value.Quantity),
            UnitPrice = item.UnitPrice,
            IsLoose = !item.ProductId.HasValue,
            Selected = requested.GetValueOrDefault(item.Id)?.Selected ?? false,
            Quantity = requested.GetValueOrDefault(item.Id)?.Quantity ?? 0,
            ReturnsToStock = item.ProductId.HasValue &&
                (requested.GetValueOrDefault(item.Id)?.ReturnsToStock ?? true)
        }).ToList();
        return Task.CompletedTask;
    }

    private static string NormalizeStatus(string? status)
    {
        return status is "active" or "voided" ? status : "all";
    }

    private void ValidateCombinedPayment(DirectSaleCreateVM viewModel, decimal total)
    {
        if (viewModel.CashAmount <= 0)
        {
            ModelState.AddModelError(
                "CombinedPayment",
                "Ingresá un monto en efectivo mayor a cero.");

            return;
        }

        if (viewModel.TransferAmount <= 0)
        {
            ModelState.AddModelError(
                "CombinedPayment",
                "Ingresá un monto en transferencia mayor a cero.");

            return;
        }

        if (viewModel.CashAmount + viewModel.TransferAmount != total)
        {
            ModelState.AddModelError(
                "CombinedPayment",
                $"La suma de efectivo y transferencia debe ser igual al total de la venta ({total:C}).");
        }
    }

    private async Task LoadAsync(DirectSaleCreateVM vm)
    {
        vm.Products = await _db.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.Quantity > 0)
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
