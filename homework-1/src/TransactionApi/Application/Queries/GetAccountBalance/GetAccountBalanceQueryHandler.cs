using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionApi.Domain;
using TransactionApi.Domain.Enums;

namespace TransactionApi.Application.Queries.GetAccountBalance;

public class GetAccountBalanceQueryHandler : IRequestHandler<GetAccountBalanceQuery, decimal>
{
    private readonly ITransactionDbContext _context;

    public GetAccountBalanceQueryHandler(ITransactionDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> Handle(GetAccountBalanceQuery request, CancellationToken cancellationToken)
    {
        var balance = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Completed)
            .AsAsyncEnumerable()
            .AggregateAsync(
                0m,
                (acc, t) =>
                {
                    if (t.ToAccount == request.AccountId)
                        return acc + t.Amount;
                    if (t.FromAccount == request.AccountId)
                        return acc - t.Amount;
                    return acc;
                },
                cancellationToken);

        return balance;
    }
}
