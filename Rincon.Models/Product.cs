using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rincon.Models;

public class Product
{
    public int Id { get; set; }
    [Timestamp]
    public uint Version { get; set; }
    [Required(ErrorMessage = "Ingrese el nombre del producto")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El precio de compra debe ser mayor a cero")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Precio de compra")]
    public decimal PurchasePrice { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "El precio de venta debe ser mayor a cero")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Precio de venta")]
    public decimal SalePrice { get; set; }

    [Required(ErrorMessage = "Ingrese la cantidad")]
    [Range(0, int.MaxValue, ErrorMessage = "La cantidad no puede ser negativa")]
    [Display(Name = "Cantidad")]
    public int Quantity { get; set; }

    [Display(Name = "Foto")]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
