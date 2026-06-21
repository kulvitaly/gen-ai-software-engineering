using Application.Common;
using Application.Tickets.Import;
using Domain.Tickets;
using MediatR;

namespace Application.Tickets;

public sealed class ImportTicketsCommandHandler(
    IEnumerable<ITicketImportParser> parsers,
    ITicketRepository repository,
    IClock clock) : IRequestHandler<ImportTicketsCommand, ApplicationResult<ImportTicketsResponse>>
{
    public async Task<ApplicationResult<ImportTicketsResponse>> Handle(ImportTicketsCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return ApplicationResult<ImportTicketsResponse>.ValidationFailure([new ApplicationError(nameof(request.Content), "Import content is required.")]);
        }

        var parser = parsers.Single(parser => parser.Format == request.Format);
        var parseResult = parser.Parse(request.Content);
        var fileErrors = parseResult.FailedRecords.Where(error => error.RecordNumber == 0).ToArray();
        if (fileErrors.Length > 0)
        {
            return ApplicationResult<ImportTicketsResponse>.ValidationFailure(fileErrors
                .SelectMany(error => error.Errors.Select(message => new ApplicationError(nameof(request.Content), message)))
                .ToArray());
        }

        var failed = parseResult.FailedRecords
            .Select(error => new ImportTicketFailure(error.RecordNumber, error.Errors))
            .ToList();
        var successful = 0;

        foreach (var record in parseResult.Records)
        {
            var ticket = Ticket.Create(ToDraft(record), clock.UtcNow);
            if (!ticket.IsValid)
            {
                failed.Add(new ImportTicketFailure(record.RecordNumber, ticket.Errors.Select(error => error.Message).ToArray()));
                continue;
            }

            await repository.Add(ticket.Value!, cancellationToken);
            successful++;
        }

        return ApplicationResult<ImportTicketsResponse>.Success(new ImportTicketsResponse(parseResult.TotalRecords, successful, failed));
    }

    private static TicketDraft ToDraft(ImportTicketRecord record)
    {
        return new TicketDraft(
            record.CustomerId,
            record.CustomerEmail,
            record.CustomerName,
            record.Subject,
            record.Description,
            record.Category,
            record.Priority,
            record.Status,
            record.Tags,
            record.Metadata,
            record.AssignedTo);
    }
}
