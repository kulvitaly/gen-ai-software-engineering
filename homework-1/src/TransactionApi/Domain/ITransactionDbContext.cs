using Microsoft.EntityFrameworkCore;
using TransactionApi.Domain.Entities;

namespace TransactionApi.Domain;

public interface ITransactionDbContext
{
    DbSet<Transaction> Transactions { get; }
    Task<int> SaveChanges(CancellationToken cancellationToken = default);
}
