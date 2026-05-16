using Application.Common;
using Domain.Tickets;
using MediatR;

namespace Application.Tickets;

public sealed record CreateTicketCommand(
    string? CustomerId,
    string? CustomerEmail,
    string? CustomerName,
    string? Subject,
    string? Description,
    TicketCategory? Category,
    TicketPriority? Priority,
    TicketStatus? Status,
    IReadOnlyCollection<string>? Tags,
    TicketMetadata? Metadata,
    string? AssignedTo = null) : IRequest<ApplicationResult<TicketDto>>;
