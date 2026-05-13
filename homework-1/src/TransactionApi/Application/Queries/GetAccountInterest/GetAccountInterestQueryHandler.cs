using MediatR;
using TransactionApi.Application.Queries.GetAccountBalance;

namespace TransactionApi.Application.Queries.GetAccountInterest;

public class GetAccountInterestQueryHandler : IRequestHandler<GetAccountInterestQuery, GetAccountInterestResult>
{
    private readonly ISender _sender;

    public GetAccountInterestQueryHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<GetAccountInterestResult> Handle(GetAccountInterestQuery request, CancellationToken cancellationToken)
    {
        if (request.Rate < 0 || request.Days < 0)
            throw new InvalidInterestParametersException("rate and days must be non-negative.");

        var principal = await _sender.Send(new GetAccountBalanceQuery(request.AccountId), cancellationToken);
        var interestRaw = principal * request.Rate * request.Days / 365m;
        var interest = decimal.Round(interestRaw, 2, MidpointRounding.ToEven);
        var principalRounded = decimal.Round(principal, 2, MidpointRounding.ToEven);

        return new GetAccountInterestResult
        {
            AccountId = request.AccountId,
            Principal = principalRounded,
            Rate = request.Rate,
            Days = request.Days,
            Interest = interest
        };
    }
}
