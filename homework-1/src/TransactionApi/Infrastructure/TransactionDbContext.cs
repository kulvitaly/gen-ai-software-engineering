using Microsoft.EntityFrameworkCore;
using TransactionApi.Domain;
using TransactionApi.Domain.Entities;
using TransactionApi.Domain.Enums;

namespace TransactionApi.Infrastructure;

public class TransactionDbContext : DbContext, ITransactionDbContext
{
    public TransactionDbContext(DbContextOptions<TransactionDbContext> options)
        : base(options)
    {
    }

    public DbSet<Transaction> Transactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FromAccount)
                .HasMaxLength(50);

            entity.Property(e => e.ToAccount)
                .HasMaxLength(50);

            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(e => e.Currency)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(e => e.Type)
                .IsRequired();

            entity.Property(e => e.Timestamp)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasDefaultValue(TransactionStatus.Pending);
        });
    }

    Task<int> ITransactionDbContext.SaveChanges(CancellationToken cancellationToken) =>
        SaveChangesAsync(cancellationToken);
}
