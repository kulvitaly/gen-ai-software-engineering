using Domain.Tickets;

namespace Application.Tickets;

internal static class TicketMapper
{
    public static TicketDto ToDto(Ticket ticket)
    {
        return new TicketDto(
            ticket.Id,
            ticket.CustomerId,
            ticket.CustomerEmail,
            ticket.CustomerName,
            ticket.Subject,
            ticket.Description,
            ticket.Category,
            ticket.Priority,
            ticket.Status,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.ResolvedAt,
            ticket.AssignedTo,
            ticket.Tags,
            ticket.Metadata);
    }
}
