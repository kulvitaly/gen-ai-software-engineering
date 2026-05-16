using System.ComponentModel.DataAnnotations;
using Application.Tickets;
using Application.Tickets.Import;
using Domain.Tickets;

namespace API.Tickets;

internal sealed record CreateTicketRequest(
    [property: Required] string? CustomerId,
    [property: Required, EmailAddress] string? CustomerEmail,
    [property: Required] string? CustomerName,
    [property: Required, StringLength(200, MinimumLength = 1)] string? Subject,
    [property: Required, StringLength(2000, MinimumLength = 10)] string? Description,
    [property: Required, TicketCategory(ErrorMessage = "Category must be a supported ticket category.")] string? Category,
    [property: Required, TicketPriority(ErrorMessage = "Priority must be a supported ticket priority.")] string? Priority,
    [property: Required, TicketStatus(ErrorMessage = "Status must be a supported ticket status.")] string? Status,
    IReadOnlyCollection<string>? Tags,
    [property: Required] TicketMetadataRequest? Metadata,
    string? AssignedTo);

internal sealed record UpdateTicketRequest(
    [property: EmailAddress] string? CustomerEmail,
    string? CustomerName,
    [property: StringLength(200, MinimumLength = 1)] string? Subject,
    [property: StringLength(2000, MinimumLength = 10)] string? Description,
    [property: TicketCategory(ErrorMessage = "Category must be a supported ticket category.")] string? Category,
    [property: TicketPriority(ErrorMessage = "Priority must be a supported ticket priority.")] string? Priority,
    [property: TicketStatus(ErrorMessage = "Status must be a supported ticket status.")] string? Status,
    IReadOnlyCollection<string>? Tags,
    TicketMetadataRequest? Metadata,
    string? AssignedTo);

internal sealed record TicketMetadataRequest(
    [property: Required, TicketSource(ErrorMessage = "Source must be a supported ticket source.")] string? Source,
    string? Browser,
    [property: Required, DeviceType(ErrorMessage = "DeviceType must be a supported device type.")] string? DeviceType);

internal sealed record TicketResponse(
    Guid Id,
    string CustomerId,
    string CustomerEmail,
    string CustomerName,
    string Subject,
    string Description,
    string Category,
    string Priority,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    string? AssignedTo,
    IReadOnlyList<string> Tags,
    TicketMetadataResponse Metadata);

internal sealed record TicketMetadataResponse(string Source, string? Browser, string DeviceType);

internal sealed record DeleteTicketApiResponse(Guid Id);

internal sealed record ImportTicketsApiResponse(int Total, int Successful, IReadOnlyList<ImportTicketFailureResponse> Failed);

internal sealed record ImportTicketFailureResponse(int RecordNumber, IReadOnlyList<string> Errors);

internal static class TicketContracts
{
    public static CreateTicketCommand ToCommand(this CreateTicketRequest request)
    {
        return new CreateTicketCommand(
            request.CustomerId,
            request.CustomerEmail,
            request.CustomerName,
            request.Subject,
            request.Description,
            request.Category.ToCategory(),
            request.Priority.ToPriority(),
            request.Status.ToStatus(),
            request.Tags,
            request.Metadata?.ToDomain(),
            request.AssignedTo);
    }

    public static UpdateTicketCommand ToCommand(this UpdateTicketRequest request, Guid id)
    {
        return new UpdateTicketCommand(
            id,
            request.CustomerEmail,
            request.CustomerName,
            request.Subject,
            request.Description,
            request.Category.ToCategory(),
            request.Priority.ToPriority(),
            request.Status.ToStatus(),
            request.Tags,
            request.Metadata?.ToDomain(),
            request.AssignedTo);
    }

    public static TicketResponse ToResponse(this TicketDto ticket)
    {
        return new TicketResponse(
            ticket.Id,
            ticket.CustomerId,
            ticket.CustomerEmail,
            ticket.CustomerName,
            ticket.Subject,
            ticket.Description,
            ticket.Category.ToApiValue(),
            ticket.Priority.ToApiValue(),
            ticket.Status.ToApiValue(),
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.ResolvedAt,
            ticket.AssignedTo,
            ticket.Tags,
            new TicketMetadataResponse(
                ticket.Metadata.Source!.Value.ToApiValue(),
                ticket.Metadata.Browser,
                ticket.Metadata.DeviceType!.Value.ToApiValue()));
    }

    public static ImportTicketsApiResponse ToResponse(this ImportTicketsResponse response)
    {
        return new ImportTicketsApiResponse(
            response.Total,
            response.Successful,
            response.Failed
                .Select(failure => new ImportTicketFailureResponse(failure.RecordNumber, failure.Errors))
                .ToArray());
    }

    public static bool TryParseImportFormat(string? value, out TicketImportFormat format)
    {
        format = default;
        if (string.Equals(value, "csv", StringComparison.OrdinalIgnoreCase))
        {
            format = TicketImportFormat.Csv;
            return true;
        }

        if (string.Equals(value, "json", StringComparison.OrdinalIgnoreCase))
        {
            format = TicketImportFormat.Json;
            return true;
        }

        if (string.Equals(value, "xml", StringComparison.OrdinalIgnoreCase))
        {
            format = TicketImportFormat.Xml;
            return true;
        }

        return false;
    }

    private static TicketMetadata ToDomain(this TicketMetadataRequest metadata)
    {
        return new TicketMetadata(
            metadata.Source.ToSource(),
            metadata.Browser,
            metadata.DeviceType.ToDeviceType());
    }
}
