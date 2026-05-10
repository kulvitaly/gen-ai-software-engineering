using MediatR;

namespace TransactionApi.Application.Queries.GetAccountBalance;

public class GetAccountBalanceQuery : IRequest<decimal>
{
    public string AccountId { get; set; }

    public GetAccountBalanceQuery(string accountId)
    {
        AccountId = accountId;
    }
}
