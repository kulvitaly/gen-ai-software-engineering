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

        // Filter by ToDate (include entire end day)
        if (request.ToDate.HasValue)
        {
            var endOfDay = request.ToDate.Value.AddDays(1).AddTicks(-1);
            query = query.Where(t => t.Timestamp <= endOfDay);
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
