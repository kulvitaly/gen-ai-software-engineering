using TransactionApi.Domain.Entities;
using TransactionApi.Domain.Enums;

namespace TransactionApi.Tests.Fixtures;

public static class TransactionTestFixture
{
    public static Transaction CreateValidTransaction(
        Guid? id = null,
        string fromAccount = "ACC-12345",
        string toAccount = "ACC-67890",
        decimal amount = 100m,
        string currency = "USD",
        TransactionType type = TransactionType.Transfer,
        DateTime? timestamp = null,
        TransactionStatus status = TransactionStatus.Completed)
    {
        return new Transaction
        {
            Id = id ?? Guid.NewGuid(),
            FromAccount = fromAccount,
            ToAccount = toAccount,
            Amount = amount,
            Currency = currency,
            Type = type,
            Timestamp = timestamp ?? DateTime.UtcNow,
            Status = status
        };
    }

    public static List<Transaction> CreateSampleTransactions()
    {
        return new List<Transaction>
        {
            CreateValidTransaction(
                id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
                fromAccount: "ACC-111",
                toAccount: "ACC-222",
                amount: 100m,
                type: TransactionType.Transfer),

            CreateValidTransaction(
                id: Guid.Parse("00000000-0000-0000-0000-000000000002"),
                fromAccount: "ACC-222",
                toAccount: "ACC-333",
                amount: 50m,
                type: TransactionType.Transfer),

            CreateValidTransaction(
                id: Guid.Parse("00000000-0000-0000-0000-000000000003"),
                fromAccount: "ACC-111",
                toAccount: "ACC-111",
                amount: 25m,
                type: TransactionType.Deposit)
        };
    }
}
