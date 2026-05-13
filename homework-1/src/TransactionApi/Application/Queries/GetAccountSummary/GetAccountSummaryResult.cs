namespace TransactionApi.Application.Queries.GetAccountSummary;

public class GetAccountSummaryResult
{
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public int TransactionCount { get; set; }
    public DateTime? MostRecentTransactionDate { get; set; }
}
