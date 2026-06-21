using Application.Common;
using MediatR;

namespace Application.Tickets;

public sealed record DeleteTicketCommand(Guid Id) : IRequest<ApplicationResult<DeleteTicketResponse>>;

public sealed record DeleteTicketResponse(Guid Id);
