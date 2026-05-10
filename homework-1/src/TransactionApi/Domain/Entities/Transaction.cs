using TransactionApi.Domain.Enums;

namespace TransactionApi.Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public string? FromAccount { get; set; }
    public string? ToAccount { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public TransactionStatus Status { get; set; }
}
