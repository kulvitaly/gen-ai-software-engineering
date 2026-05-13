using MediatR;
using TransactionApi.Application.DTOs;

namespace TransactionApi.Application.Queries.GetTransactionById;

public class GetTransactionByIdQuery : IRequest<TransactionDto?>
{
    public Guid Id { get; set; }

    public GetTransactionByIdQuery(Guid id)
    {
        Id = id;
    }
}
