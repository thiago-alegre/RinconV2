using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rincon.DataAccess.Data;
using Rincon.Infrastructure;
using Rincon.Models;
using Rincon.Utilities;

namespace Rincon.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = SD.Role_Admin)]
public class ArticlesController : Controller
{
    private const long MaxImageSizeBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public ArticlesController(ApplicationDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Upsert(int? id)
    {
        if (!id.HasValue)
        {
            return View(new Product());
        }

        var product = await _db.Products.FindAsync(id.Value);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Upsert(Product input, IFormFile? image)
    {
        var product = await FindProductForUpdateAsync(input.Id);

        if (input.Id != 0 && product is null)
        {
            return NotFound();
        }

        ValidatePrices(input);
        ValidateImage(image);

        if (!ModelState.IsValid)
        {
            input.ImageUrl = product?.ImageUrl;
            return View(input);
        }

        product ??= new Product();
        ApplyEditableFields(product, input);

        var previousImageUrl = product.ImageUrl;
        string? newImageUrl = null;

        if (image is not null)
        {
            newImageUrl = await SaveImageAsync(image);
            product.ImageUrl = newImageUrl;
        }

        if (product.Id == 0)
        {
            _db.Products.Add(product);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            DeleteImage(newImageUrl);
            throw;
        }

        if (newImageUrl is not null)
        {
            DeleteImage(previousImageUrl);
        }

        TempData["success"] = "Producto guardado correctamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var request = DataTableRequest.From(Request);
        var query = _db.Products
            .AsNoTracking()
            .Select(product => new
            {
                product.Id,
                product.Name,
                product.Description,
                product.Quantity,
                product.PurchasePrice,
                product.SalePrice,
                product.ImageUrl,
                product.IsActive
            });

        var recordsTotal = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = DataTableSearchTerm.Create(request.Search);
            var matchesActive = search.EqualsLabel("Activo") ||
                (!search.EqualsLabel("Inactivo") && search.MatchesLabel("Activo"));
            var matchesInactive = search.EqualsLabel("Inactivo") ||
                (!search.EqualsLabel("Activo") && search.MatchesLabel("Inactivo"));

            query = query.Where(product =>
                EF.Functions.ILike(product.Name, search.Pattern) ||
                (product.Description != null && EF.Functions.ILike(product.Description, search.Pattern)) ||
                (search.HasNumber && product.Quantity == search.Number) ||
                (search.HasNumber && product.PurchasePrice == search.Number) ||
                (search.HasNumber && product.SalePrice == search.Number) ||
                (search.HasNumber && product.SalePrice - product.PurchasePrice == search.Number) ||
                (matchesActive && product.IsActive) ||
                (matchesInactive && !product.IsActive));
        }

        var recordsFiltered = await query.CountAsync();

        query = request.OrderColumn switch
        {
            1 => request.IsAscending
                ? query.OrderBy(product => product.Name)
                : query.OrderByDescending(product => product.Name),
            2 => request.IsAscending
                ? query.OrderBy(product => product.Quantity)
                : query.OrderByDescending(product => product.Quantity),
            3 => request.IsAscending
                ? query.OrderBy(product => product.PurchasePrice)
                : query.OrderByDescending(product => product.PurchasePrice),
            4 => request.IsAscending
                ? query.OrderBy(product => product.SalePrice)
                : query.OrderByDescending(product => product.SalePrice),
            5 => request.IsAscending
                ? query.OrderBy(product => product.SalePrice - product.PurchasePrice)
                : query.OrderByDescending(product => product.SalePrice - product.PurchasePrice),
            6 => request.IsAscending
                ? query.OrderBy(product => product.IsActive)
                : query.OrderByDescending(product => product.IsActive),
            _ => query.OrderBy(product => product.Name)
        };

        var products = await query
            .Skip(request.Start)
            .Take(request.Length)
            .ToListAsync();

        return Json(new
        {
            draw = request.Draw,
            recordsTotal,
            recordsFiltered,
            data = products
        });
    }

    [HttpDelete, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);

        if (product is null)
        {
            return Json(new { success = false, message = "Producto no encontrado" });
        }

        product.IsActive = false;
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Producto desactivado" });
    }

    private async Task<Product?> FindProductForUpdateAsync(int productId)
    {
        return productId == 0
            ? null
            : await _db.Products.FirstOrDefaultAsync(product => product.Id == productId);
    }

    private void ValidatePrices(Product product)
    {
        if (product.SalePrice < product.PurchasePrice)
        {
            ModelState.AddModelError(
                nameof(product.SalePrice),
                "El precio de venta no puede ser menor al precio de compra");
        }
    }

    private void ValidateImage(IFormFile? image)
    {
        if (image is null)
        {
            return;
        }

        var extension = Path.GetExtension(image.FileName);
        var isValid = image.Length is > 0 and <= MaxImageSizeBytes
            && image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            && AllowedImageExtensions.Contains(extension);

        if (!isValid)
        {
            ModelState.AddModelError(
                nameof(Product.ImageUrl),
                "La imagen debe ser JPG, PNG o WEBP y pesar como máximo 2 MB");
        }
    }

    private static void ApplyEditableFields(Product product, Product input)
    {
        product.Name = input.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(input.Description)
            ? null
            : input.Description.Trim();
        product.PurchasePrice = input.PurchasePrice;
        product.SalePrice = input.SalePrice;
        product.Quantity = input.Quantity;
        product.IsActive = input.IsActive;
    }

    private async Task<string> SaveImageAsync(IFormFile image)
    {
        var imageDirectory = Path.Combine(_environment.WebRootPath, "imagenes", "products");
        Directory.CreateDirectory(imageDirectory);

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(imageDirectory, fileName);

        await using var stream = System.IO.File.Create(filePath);
        await image.CopyToAsync(stream);

        return $"/imagenes/products/{fileName}";
    }

    private void DeleteImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        var relativePath = imageUrl
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);
        var filePath = Path.Combine(_environment.WebRootPath, relativePath);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }
}
