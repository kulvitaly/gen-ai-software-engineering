using MediatR;

namespace TransactionApi.Application.Queries.GetAccountInterest;

public class GetAccountInterestQuery : IRequest<GetAccountInterestResult>
{
    public GetAccountInterestQuery(string accountId, decimal rate, int days)
    {
        AccountId = accountId;
        Rate = rate;
        Days = days;
    }

    public string AccountId { get; }
    public decimal Rate { get; }
    public int Days { get; }
}
