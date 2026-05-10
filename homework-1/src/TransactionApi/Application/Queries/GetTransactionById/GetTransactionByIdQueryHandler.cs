using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionApi.Application.DTOs;
using TransactionApi.Domain;

namespace TransactionApi.Application.Queries.GetTransactionById;

public class GetTransactionByIdQueryHandler : IRequestHandler<GetTransactionByIdQuery, TransactionDto?>
{
    private readonly ITransactionDbContext _context;

    public GetTransactionByIdQueryHandler(ITransactionDbContext context)
    {
        _context = context;
    }

    public async Task<TransactionDto?> Handle(GetTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var transaction = await _context.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (transaction == null)
            return null;

        return new TransactionDto
        {
            Id = transaction.Id,
            FromAccount = transaction.FromAccount,
            ToAccount = transaction.ToAccount,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Type = transaction.Type.ToString(),
            Timestamp = transaction.Timestamp,
            Status = transaction.Status.ToString()
        };
    }
}
