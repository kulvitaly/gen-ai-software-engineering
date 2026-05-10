using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionApi.Domain;
using TransactionApi.Domain.Enums;

namespace TransactionApi.Application.Queries.GetAccountSummary;

public class GetAccountSummaryQueryHandler : IRequestHandler<GetAccountSummaryQuery, GetAccountSummaryResult>
{
    private readonly ITransactionDbContext _context;

    public GetAccountSummaryQueryHandler(ITransactionDbContext context)
    {
        _context = context;
    }

    public async Task<GetAccountSummaryResult> Handle(GetAccountSummaryQuery request, CancellationToken cancellationToken)
    {
        var accountId = request.AccountId;

        var involved = _context.Transactions.AsNoTracking()
            .Where(t => t.FromAccount == accountId || t.ToAccount == accountId);

        var completed = involved.Where(t => t.Status == TransactionStatus.Completed);

        var totalDeposits = await completed
            .Where(t => t.Type == TransactionType.Deposit && t.ToAccount == accountId)
            .SumAsync(t => t.Amount, cancellationToken);

        var totalWithdrawals = await completed
            .Where(t => t.Type == TransactionType.Withdrawal && t.FromAccount == accountId)
            .SumAsync(t => t.Amount, cancellationToken);

        var transactionCount = await involved.CountAsync(cancellationToken);

        var mostRecent = await involved.Select(t => (DateTime?)t.Timestamp).MaxAsync(cancellationToken);

        return new GetAccountSummaryResult
        {
            TotalDeposits = totalDeposits,
            TotalWithdrawals = totalWithdrawals,
            TransactionCount = transactionCount,
            MostRecentTransactionDate = mostRecent
        };
    }
}
