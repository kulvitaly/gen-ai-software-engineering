using MediatR;
using TransactionApi.Application.Csv;
using TransactionApi.Application.Queries.GetAllTransactions;

namespace TransactionApi.Application.Queries.ExportTransactionsCsv;

public class ExportTransactionsCsvQueryHandler : IRequestHandler<ExportTransactionsCsvQuery, string>
{
    private readonly ISender _sender;

    public ExportTransactionsCsvQueryHandler(ISender sender)
    {
        _sender = sender;
    }

    public async Task<string> Handle(ExportTransactionsCsvQuery request, CancellationToken cancellationToken)
    {
        var transactions = await _sender.Send(
            new GetTransactionsByFilterQuery(),
            cancellationToken);

        return TransactionCsvExporter.ToCsv(transactions);
    }
}
