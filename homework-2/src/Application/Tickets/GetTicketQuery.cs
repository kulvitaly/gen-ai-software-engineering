using Application.Common;
using MediatR;

namespace Application.Tickets;

public sealed record GetTicketQuery(Guid Id) : IRequest<ApplicationResult<TicketDto>>;
