namespace TransactionApi.Application.DTOs;

public class TransactionDto
{
    public Guid Id { get; set; }
    public string? FromAccount { get; set; }
    public string? ToAccount { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
}
