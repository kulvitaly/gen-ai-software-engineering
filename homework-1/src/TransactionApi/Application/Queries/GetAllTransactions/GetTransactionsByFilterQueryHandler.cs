using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionApi.Application.DTOs;
using TransactionApi.Domain;

namespace TransactionApi.Application.Queries.GetAllTransactions;

public class GetTransactionsByFilterQueryHandler : IRequestHandler<GetTransactionsByFilterQuery, List<TransactionDto>>
{
    private readonly ITransactionDbContext _context;

    public GetTransactionsByFilterQueryHandler(ITransactionDbContext context)
    {
        _context = context;
    }

    public async Task<List<TransactionDto>> Handle(GetTransactionsByFilterQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Transactions.AsNoTracking();

        // Filter by AccountId (matches both FromAccount OR ToAccount)
        if (!string.IsNullOrEmpty(request.AccountId))
        {
            query = query.Where(t => t.FromAccount == request.AccountId || t.ToAccount == request.AccountId);
        }

        // Filter by FromDate
        if (request.FromDate.HasValue)
        {
            query = query.Where(t => t.Timestamp >= request.FromDate.Value);
        }

        // Filter by ToDate: when the bound is date-only (midnight), include the whole calendar day;
        // otherwise treat the value as an exact inclusive upper instant (e.g. ...T23:59:59Z).
        if (request.ToDate.HasValue)
        {
            var to = request.ToDate.Value;
            var upperInclusive = to.TimeOfDay == TimeSpan.Zero
                ? to.Date.AddDays(1).AddTicks(-1)
                : to;
            query = query.Where(t => t.Timestamp <= upperInclusive);
        }

        var transactions = await query
            .OrderByDescending(t => t.Timestamp)
            .ToListAsync(cancellationToken);

        // Filter by Type (case-insensitive, applied client-side)
        if (!string.IsNullOrEmpty(request.Type))
        {
            transactions = transactions
                .Where(t => t.Type.ToString().Equals(request.Type, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return transactions.Select(t => new TransactionDto
        {
            Id = t.Id,
            FromAccount = t.FromAccount,
            ToAccount = t.ToAccount,
            Amount = t.Amount,
            Currency = t.Currency,
            Type = t.Type.ToString(),
            Timestamp = t.Timestamp,
            Status = t.Status.ToString()
        }).ToList();
    }
}
