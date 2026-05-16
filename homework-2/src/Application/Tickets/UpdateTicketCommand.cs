using Application.Common;
using Domain.Tickets;
using MediatR;

namespace Application.Tickets;

public sealed record UpdateTicketCommand(
    Guid Id,
    string? CustomerEmail = null,
    string? CustomerName = null,
    string? Subject = null,
    string? Description = null,
    TicketCategory? Category = null,
    TicketPriority? Priority = null,
    TicketStatus? Status = null,
    IReadOnlyCollection<string>? Tags = null,
    TicketMetadata? Metadata = null,
    string? AssignedTo = null) : IRequest<ApplicationResult<TicketDto>>;
