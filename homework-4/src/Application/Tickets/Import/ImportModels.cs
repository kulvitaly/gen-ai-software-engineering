using Domain.Tickets;

namespace Application.Tickets.Import;

public enum TicketImportFormat
{
    Csv,
    Json,
    Xml
}

public sealed record ImportTicketRecord(
    int RecordNumber,
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
    string? AssignedTo);

public sealed record ImportRecordError(int RecordNumber, IReadOnlyList<string> Errors);

public sealed record TicketImportParseResult(
    int TotalRecords,
    IReadOnlyList<ImportTicketRecord> Records,
    IReadOnlyList<ImportRecordError> FailedRecords);

public interface ITicketImportParser
{
    TicketImportFormat Format { get; }

    TicketImportParseResult Parse(string content);
}
