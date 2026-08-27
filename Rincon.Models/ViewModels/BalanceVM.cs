namespace Rincon.Models.ViewModels;

public class BalanceVM
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public decimal CollectedSales { get; set; }
    public decimal PersonalAccountCollections { get; set; }
    public decimal OutstandingPersonalAccounts { get; set; }
    public decimal MerchandisePurchases { get; set; }
    public decimal BusinessExpenses { get; set; }
    public decimal PersonalWithdrawals { get; set; }
    public decimal AvailableCash { get; set; }
    public decimal AvailableTransfer { get; set; }
    public decimal AvailableBalance => AvailableCash + AvailableTransfer;
    public decimal EstimatedProfit { get; set; }
    public List<Expense> RecentExpenses { get; set; } = new();
}
