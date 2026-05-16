using Domain.Tickets;

namespace Application.Tickets;

public sealed record TicketDto(
    Guid Id,
    string CustomerId,
    string CustomerEmail,
    string CustomerName,
    string Subject,
    string Description,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    string? AssignedTo,
    IReadOnlyList<string> Tags,
    TicketMetadata Metadata,
    TicketClassification? Classification);

public sealed record ClassificationDto(
    TicketCategory Category,
    TicketPriority Priority,
    double Confidence,
    string Reasoning,
    IReadOnlyList<string> KeywordsFound);
