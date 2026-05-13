using MediatR;
using TransactionApi.Application.DTOs;

namespace TransactionApi.Application.Queries.GetAllTransactions;

public class GetTransactionsByFilterQuery : IRequest<List<TransactionDto>>
{
    public string? AccountId { get; set; }
    public string? Type { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
