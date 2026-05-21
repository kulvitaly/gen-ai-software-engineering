namespace Domain.Tickets;

public sealed record ValidationError(string Field, string Message);
