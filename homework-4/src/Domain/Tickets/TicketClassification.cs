namespace Domain.Tickets;

public sealed record TicketClassification(
    TicketCategory Category,
    TicketPriority Priority,
    double Confidence,
    string Reasoning,
    IReadOnlyList<string> KeywordsFound);
