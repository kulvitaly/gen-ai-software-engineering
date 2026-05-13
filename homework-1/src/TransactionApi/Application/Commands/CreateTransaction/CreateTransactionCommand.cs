using MediatR;

namespace TransactionApi.Application.Commands.CreateTransaction;

public class CreateTransactionCommand : IRequest<Guid>
{
    public string? FromAccount { get; set; }
    public string? ToAccount { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
