using System.Xml;
using System.Xml.Linq;
using Domain.Tickets;

namespace Application.Tickets.Import;

public sealed class XmlTicketImportParser : ITicketImportParser
{
    public TicketImportFormat Format => TicketImportFormat.Xml;

    public TicketImportParseResult Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new TicketImportParseResult(0, [], [new ImportRecordError(0, ["XML file is empty."])]);
        }

        try
        {
            var document = XDocument.Parse(content);
            if (document.Root?.Name.LocalName != "tickets")
            {
                return new TicketImportParseResult(0, [], [new ImportRecordError(0, ["XML root element must be tickets."])]);
            }

            var ticketElements = document.Root.Elements("ticket").ToArray();
            if (ticketElements.Length == 0)
            {
                return new TicketImportParseResult(0, [], [new ImportRecordError(0, ["XML must contain at least one ticket element."])]);
            }

            var records = ticketElements
                .Select((ticket, index) => ToRecord(ticket, index + 1))
                .ToArray();

            return new TicketImportParseResult(records.Length, records, []);
        }
        catch (XmlException exception)
        {
            return new TicketImportParseResult(0, [], [new ImportRecordError(0, [$"XML is malformed: {exception.Message}"])]);
        }
    }

    private static ImportTicketRecord ToRecord(XElement ticket, int recordNumber)
    {
        var metadata = ticket.Element("metadata");

        return new ImportTicketRecord(
            recordNumber,
            Value(ticket, "customer_id"),
            Value(ticket, "customer_email"),
            Value(ticket, "customer_name"),
            Value(ticket, "subject"),
            Value(ticket, "description"),
            TicketImportValueParser.ToCategory(Value(ticket, "category")),
            TicketImportValueParser.ToPriority(Value(ticket, "priority")),
            TicketImportValueParser.ToStatus(Value(ticket, "status")),
            Tags(ticket),
            new TicketMetadata(
                TicketImportValueParser.ToSource(Value(metadata, "source")),
                Value(metadata, "browser"),
                TicketImportValueParser.ToDeviceType(Value(metadata, "device_type"))),
            Value(ticket, "assigned_to"));
    }

    private static string? Value(XContainer? container, string name)
    {
        var value = container?.Element(name)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyCollection<string> Tags(XContainer ticket)
    {
        return ticket
            .Element("tags")?
            .Elements("tag")
            .Select(tag => tag.Value)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .ToArray() ?? [];
    }
}
