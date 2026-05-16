namespace Domain.Tickets;

public sealed record TicketDraft(
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
    string? AssignedTo = null);
