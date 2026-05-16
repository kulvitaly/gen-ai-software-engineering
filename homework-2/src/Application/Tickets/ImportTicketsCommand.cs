using Application.Common;
using Application.Tickets.Import;
using MediatR;

namespace Application.Tickets;

public sealed record ImportTicketsCommand(TicketImportFormat Format, string Content) : IRequest<ApplicationResult<ImportTicketsResponse>>;

public sealed record ImportTicketsResponse(int Total, int Successful, IReadOnlyList<ImportTicketFailure> Failed);

public sealed record ImportTicketFailure(int RecordNumber, IReadOnlyList<string> Errors);
