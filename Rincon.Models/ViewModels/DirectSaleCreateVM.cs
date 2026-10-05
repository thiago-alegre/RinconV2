using Rincon.Utilities.Enums;
using System.ComponentModel.DataAnnotations;

namespace Rincon.Models.ViewModels;

public class DirectSaleCreateVM
{
    public Guid OperationId { get; set; }
    [Display(Name = "Fecha")]
    public DateTime Date { get; set; } = DateTime.Now;
    [Display(Name = "Forma de pago")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Efectivo;

    [Display(Name = "Monto en efectivo")]
    public decimal CashAmount { get; set; }

    [Display(Name = "Monto en transferencia")]
    public decimal TransferAmount { get; set; }

    [Display(Name = "Cuenta personal")]
    public int? PersonalAccountId { get; set; }
    public List<DirectSaleLineVM> Lines { get; set; } = new() { new() };
    public List<Product> Products { get; set; } = new();
    public List<PersonalAccount> Accounts { get; set; } = new();
}

public class DirectSaleVoidVM
{
    public bool IsAccountSale { get; set; }
    public Guid OperationId { get; set; }
    public int SaleId { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public PaymentMethod RefundMethod { get; set; } = PaymentMethod.Efectivo;

    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres")]
    public string? Reason { get; set; }

    public List<DirectSaleVoidLineVM> Lines { get; set; } = new();
}

public class DirectSaleVoidLineVM
{
    public bool Selected { get; set; }
    public int DirectSaleItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal SoldQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsLoose { get; set; }
    public bool ReturnsToStock { get; set; } = true;
    public decimal AvailableQuantity => SoldQuantity - ReturnedQuantity;
}

public class DirectSaleLineVM
{
    [Display(Name = "Producto")]
    public int? ProductId { get; set; }

    public bool IsLoose { get; set; }

    [StringLength(200, ErrorMessage = "La descripción no puede superar los 200 caracteres")]
    [Display(Name = "Descripción")]
    public string? LooseName { get; set; }

    [Display(Name = "Precio unitario")]
    public decimal LooseUnitPrice { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser un número entero mayor a cero")]
    [Display(Name = "Cantidad")]
    public int Quantity { get; set; } = 1;
}
