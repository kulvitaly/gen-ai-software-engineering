namespace TransactionApi.Application.Queries.GetAccountInterest;

public class GetAccountInterestResult
{
    public required string AccountId { get; init; }
    public decimal Principal { get; init; }
    public decimal Rate { get; init; }
    public int Days { get; init; }
    public decimal Interest { get; init; }
}
