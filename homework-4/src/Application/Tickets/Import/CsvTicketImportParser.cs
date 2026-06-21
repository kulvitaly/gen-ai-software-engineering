using Domain.Tickets;

namespace Application.Tickets.Import;

public sealed class CsvTicketImportParser : ITicketImportParser
{
    private static readonly string[] RequiredHeaders =
    [
        "customer_id",
        "customer_email",
        "customer_name",
        "subject",
        "description",
        "category",
        "priority",
        "status",
        "metadata.source",
        "metadata.device_type"
    ];

    public TicketImportFormat Format => TicketImportFormat.Csv;

    public TicketImportParseResult Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new TicketImportParseResult(0, [], [new ImportRecordError(0, ["CSV file is empty."])]);
        }

        var rows = ParseRows(content);
        if (rows.Count == 0)
        {
            return new TicketImportParseResult(0, [], [new ImportRecordError(0, ["CSV file is empty."])]);
        }

        var headers = rows[0].Select(header => header.Trim()).ToArray();
        var missingHeaders = RequiredHeaders
            .Where(header => !headers.Contains(header, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (missingHeaders.Length > 0)
        {
            return new TicketImportParseResult(0, [], [new ImportRecordError(0, [$"CSV is missing required headers: {string.Join(", ", missingHeaders)}."])]);
        }

        var records = new List<ImportTicketRecord>();
        var failedRecords = new List<ImportRecordError>();
        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var recordNumber = rowIndex;
            var row = rows[rowIndex];
            if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0]))
            {
                continue;
            }

            if (row.Count != headers.Length)
            {
                failedRecords.Add(new ImportRecordError(recordNumber, [$"CSV row has {row.Count} columns but expected {headers.Length}."]));
                continue;
            }

            var values = headers.Zip(row).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.OrdinalIgnoreCase);
            records.Add(new ImportTicketRecord(
                recordNumber,
                Get(values, "customer_id"),
                Get(values, "customer_email"),
                Get(values, "customer_name"),
                Get(values, "subject"),
                Get(values, "description"),
                TicketImportValueParser.ToCategory(Get(values, "category")),
                TicketImportValueParser.ToPriority(Get(values, "priority")),
                TicketImportValueParser.ToStatus(Get(values, "status")),
                TicketImportValueParser.ToTags(Get(values, "tags")),
                new TicketMetadata(
                    TicketImportValueParser.ToSource(Get(values, "metadata.source")),
                    Get(values, "metadata.browser"),
                    TicketImportValueParser.ToDeviceType(Get(values, "metadata.device_type"))),
                Get(values, "assigned_to")));
        }

        return new TicketImportParseResult(records.Count + failedRecords.Count, records, failedRecords);
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
    }

    private static List<List<string>> ParseRows(string content)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new List<char>();
        var inQuotes = false;

        for (var index = 0; index < content.Length; index++)
        {
            var current = content[index];
            if (current == '"')
            {
                if (inQuotes && index + 1 < content.Length && content[index + 1] == '"')
                {
                    field.Add('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (!inQuotes && current == ',')
            {
                AddField(row, field);
                continue;
            }

            if (!inQuotes && (current == '\r' || current == '\n'))
            {
                if (current == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }

                AddField(row, field);
                rows.Add(row);
                row = [];
                continue;
            }

            field.Add(current);
        }

        AddField(row, field);
        if (row.Count > 1 || row.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            rows.Add(row);
        }

        return rows;
    }

    private static void AddField(ICollection<string> row, List<char> field)
    {
        row.Add(new string(field.ToArray()));
        field.Clear();
    }
}
