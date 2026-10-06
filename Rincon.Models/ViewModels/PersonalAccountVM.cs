using Rincon.Utilities.Enums;

namespace Rincon.Models.ViewModels
{
    public class PersonalAccountDetailVM
    {
        public PersonalAccount Account { get; set; } = new();
        public IEnumerable<DirectSale> Sales { get; set; } = new List<DirectSale>();
        public IEnumerable<PersonalAccountPayment> Payments { get; set; } = new List<PersonalAccountPayment>();
        public decimal CurrentDebt { get; set; }
        public decimal CurrentCredit { get; set; }
        public Guid PaymentOperationId { get; set; } = Guid.NewGuid();
        public DateTime? DebtSince { get; set; }
    }

    public class PersonalAccountSettleVM
    {
        public Guid OperationId { get; set; }
        public int Id { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? AmountText { get; set; }
        public string? Notes { get; set; }
    }

    public class PersonalAccountPaymentChangeVM
    {
        public int PaymentId { get; set; }
        public int AccountId { get; set; }
        public decimal OriginalAmount { get; set; }
        public DateTime OriginalDate { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public PaymentMethod PaymentMethod { get; set; }
        public string? AmountText { get; set; }
        public string? Notes { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
