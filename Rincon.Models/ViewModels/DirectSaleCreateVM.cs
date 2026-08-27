using Rincon.Utilities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Rincon.Models.ViewModels;

public class DirectSaleCreateVM
{
    [Display(Name = "Fecha")]
    public DateTime Date { get; set; } = DateTime.Now;
    [Display(Name = "Forma de pago")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Efectivo;
    [Display(Name = "Cuenta personal")]
    public int? PersonalAccountId { get; set; }
    public List<DirectSaleLineVM> Lines { get; set; } = new() { new() };
    public List<Product> Products { get; set; } = new();
    public List<PersonalAccount> Accounts { get; set; } = new();
}

public class DirectSaleLineVM
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione un producto")]
    [Display(Name = "Producto")]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser un número entero mayor a cero")]
    [Display(Name = "Cantidad")]
    public int Quantity { get; set; } = 1;
}
