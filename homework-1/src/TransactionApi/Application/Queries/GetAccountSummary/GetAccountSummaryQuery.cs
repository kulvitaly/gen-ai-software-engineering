using MediatR;

namespace TransactionApi.Application.Queries.GetAccountSummary;

public class GetAccountSummaryQuery : IRequest<GetAccountSummaryResult>
{
    public GetAccountSummaryQuery(string accountId)
    {
        AccountId = accountId;
    }

    public string AccountId { get; }
}
