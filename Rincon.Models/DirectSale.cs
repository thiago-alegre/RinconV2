using Rincon.Utilities.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rincon.Models;

public class DirectSale
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public int? PersonalAccountId { get; set; }
    public PersonalAccount? PersonalAccount { get; set; }
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public bool IsVoided { get; set; }
    public DateTime? VoidedAt { get; set; }

    [StringLength(500)]
    public string? VoidReason { get; set; }

    public string? VoidedByUserId { get; set; }

    [ForeignKey(nameof(VoidedByUserId))]
    public ApplicationUser? VoidedByUser { get; set; }

    public ICollection<DirectSaleItem> Items { get; set; } = new List<DirectSaleItem>();
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
}
