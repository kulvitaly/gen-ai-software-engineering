namespace Domain.Tickets;

public sealed record TicketMetadata(TicketSource? Source, string? Browser, DeviceType? DeviceType);
