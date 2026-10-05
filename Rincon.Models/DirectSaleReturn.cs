using Rincon.Utilities.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rincon.Models;

public class DirectSaleReturn
{
    public int Id { get; set; }
    public Guid? OperationId { get; set; }
    public int DirectSaleId { get; set; }
    public DirectSale DirectSale { get; set; } = null!;
    public DateTime Date { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    public PaymentMethod RefundMethod { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    public string? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public ICollection<DirectSaleReturnItem> Items { get; set; } = new List<DirectSaleReturnItem>();
}

public class DirectSaleReturnItem
{
    public int Id { get; set; }
    public int DirectSaleReturnId { get; set; }
    public DirectSaleReturn DirectSaleReturn { get; set; } = null!;
    public int DirectSaleItemId { get; set; }
    public DirectSaleItem DirectSaleItem { get; set; } = null!;
    public int? ProductId { get; set; }
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

    public bool ReturnsToStock { get; set; }
}
