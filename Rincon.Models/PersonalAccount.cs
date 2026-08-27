using System.ComponentModel.DataAnnotations;

namespace Rincon.Models
{
    public class PersonalAccount
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Ingrese el nombre completo")]
        [Display(Name = "Nombre completo")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese el DNI")]
        [Display(Name = "DNI")]
        public string DNI { get; set; } = string.Empty;

        [Display(Name = "Dirección")]
        public string? Address { get; set; }

        [Display(Name = "Teléfono")]
        public string? Phone { get; set; }

        [Display(Name = "Fecha de alta")]
        public DateTime Date { get; set; } = DateTime.Now;

        [Display(Name = "Estado")]
        public bool isActive { get; set; } = true;

        public ICollection<DirectSale> Sales { get; set; } = new List<DirectSale>();
        public ICollection<PersonalAccountPayment> Payments { get; set; } = new List<PersonalAccountPayment>();
    }
}
