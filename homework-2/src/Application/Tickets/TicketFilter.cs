using Domain.Tickets;

namespace Application.Tickets;

public sealed record TicketFilter(
    TicketCategory? Category = null,
    TicketPriority? Priority = null,
    TicketStatus? Status = null);
