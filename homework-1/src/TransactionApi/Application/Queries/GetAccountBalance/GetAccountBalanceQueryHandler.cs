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
        var accountId = request.AccountId;

        return await _context.Transactions
            .AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Completed
                        && (t.ToAccount == accountId || t.FromAccount == accountId))
            .SumAsync(
                t => (t.ToAccount == accountId ? t.Amount : 0m)
                     + (t.FromAccount == accountId ? -t.Amount : 0m),
                cancellationToken);
    }
}
