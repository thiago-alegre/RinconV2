using Rincon.Utilities.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rincon.Models;

public class DirectSale
{
    public int Id { get; set; }
    public Guid? OperationId { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CashAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TransferAmount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public int? PersonalAccountId { get; set; }
    public PersonalAccount? PersonalAccount { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public ICollection<DirectSaleItem> Items { get; set; } = new List<DirectSaleItem>();
    public ICollection<DirectSaleReturn> Returns { get; set; } = new List<DirectSaleReturn>();

}

public class DirectSaleItem
{
    public int Id { get; set; }
    public int DirectSaleId { get; set; }
    public DirectSale DirectSale { get; set; } = null!;
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string ProductName { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    public ICollection<DirectSaleReturnItem> ReturnItems { get; set; } = new List<DirectSaleReturnItem>();
}
