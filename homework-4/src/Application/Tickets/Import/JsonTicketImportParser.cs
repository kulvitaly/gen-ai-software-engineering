using System.Text.Json;
using Domain.Tickets;

namespace Application.Tickets.Import;

public sealed class JsonTicketImportParser : ITicketImportParser
{
    public TicketImportFormat Format => TicketImportFormat.Json;

    public TicketImportParseResult Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new TicketImportParseResult(0, [], [new ImportRecordError(0, ["JSON file is empty."])]);
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var tickets = GetTicketElements(root, out var schemaError);
            if (schemaError is not null)
            {
                return new TicketImportParseResult(0, [], [new ImportRecordError(0, [schemaError])]);
            }

            var records = tickets!
                .Select((ticket, index) => ToRecord(ticket, index + 1))
                .ToArray();

            return new TicketImportParseResult(records.Length, records, []);
        }
        catch (JsonException exception)
        {
            return new TicketImportParseResult(0, [], [new ImportRecordError(0, [$"JSON is malformed: {exception.Message}"])]);
        }
    }

    private static JsonElement[]? GetTicketElements(JsonElement root, out string? error)
    {
        error = null;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().ToArray();
        }

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("tickets", out var tickets)
            && tickets.ValueKind == JsonValueKind.Array)
        {
            return tickets.EnumerateArray().ToArray();
        }

        error = "JSON must be an array of tickets or an object with a tickets array.";
        return null;
    }

    private static ImportTicketRecord ToRecord(JsonElement ticket, int recordNumber)
    {
        var metadata = GetObject(ticket, "metadata");

        return new ImportTicketRecord(
            recordNumber,
            GetString(ticket, "customer_id"),
            GetString(ticket, "customer_email"),
            GetString(ticket, "customer_name"),
            GetString(ticket, "subject"),
            GetString(ticket, "description"),
            TicketImportValueParser.ToCategory(GetString(ticket, "category")),
            TicketImportValueParser.ToPriority(GetString(ticket, "priority")),
            TicketImportValueParser.ToStatus(GetString(ticket, "status")),
            GetTags(ticket),
            new TicketMetadata(
                TicketImportValueParser.ToSource(GetString(metadata, "source")),
                GetString(metadata, "browser"),
                TicketImportValueParser.ToDeviceType(GetString(metadata, "device_type"))),
            GetString(ticket, "assigned_to"));
    }

    private static JsonElement GetObject(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Object
                ? value
                : default;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private static IReadOnlyCollection<string> GetTags(JsonElement ticket)
    {
        if (!ticket.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return tags
            .EnumerateArray()
            .Where(tag => tag.ValueKind == JsonValueKind.String)
            .Select(tag => tag.GetString())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .ToArray();
    }
}
