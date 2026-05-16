using Application.Common;
using Domain.Tickets;
using MediatR;

namespace Application.Tickets;

public sealed record ListTicketsQuery(
    TicketCategory? Category = null,
    TicketPriority? Priority = null,
    TicketStatus? Status = null) : IRequest<ApplicationResult<IReadOnlyList<TicketDto>>>;
