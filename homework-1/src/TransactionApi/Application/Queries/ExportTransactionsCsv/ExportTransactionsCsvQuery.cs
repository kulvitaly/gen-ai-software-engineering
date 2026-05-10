using MediatR;

namespace TransactionApi.Application.Queries.ExportTransactionsCsv;

public class ExportTransactionsCsvQuery : IRequest<string>
{
}
