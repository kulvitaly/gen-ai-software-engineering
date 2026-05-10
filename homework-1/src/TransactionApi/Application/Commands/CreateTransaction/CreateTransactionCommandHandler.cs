using MediatR;
using TransactionApi.Domain;
using TransactionApi.Domain.Entities;
using TransactionApi.Domain.Enums;

namespace TransactionApi.Application.Commands.CreateTransaction;

public class CreateTransactionCommandHandler : IRequestHandler<CreateTransactionCommand, Guid>
{
    private readonly ITransactionDbContext _context;

    public CreateTransactionCommandHandler(ITransactionDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            FromAccount = string.IsNullOrWhiteSpace(request.FromAccount) ? null : request.FromAccount.Trim(),
            ToAccount = string.IsNullOrWhiteSpace(request.ToAccount) ? null : request.ToAccount.Trim(),
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            Type = Enum.Parse<TransactionType>(request.Type, ignoreCase: true),
            Timestamp = DateTime.UtcNow,
            Status = TransactionStatus.Completed
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChanges(cancellationToken);

        return transaction.Id;
    }
}
