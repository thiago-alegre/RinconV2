using Rincon.Utilities.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Rincon.Models;

public class Expense
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Display(Name = "Fecha")]
    public DateTime Date { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Seleccione el tipo de salida")]
    [Display(Name = "Tipo")]
    public ExpenseType Type { get; set; }

    [Required(ErrorMessage = "Ingrese un concepto")]
    [StringLength(120)]
    [Display(Name = "Concepto")]
    public string Concept { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingrese un monto")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero")]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Monto")]
    public decimal Amount { get; set; }

    [Required]
    [Display(Name = "Forma de pago")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Efectivo;

    [StringLength(500)]
    [Display(Name = "Notas")]
    public string? Notes { get; set; }

    public string? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public bool IsVoided { get; set; }
    public DateTime? VoidedAt { get; set; }
}
