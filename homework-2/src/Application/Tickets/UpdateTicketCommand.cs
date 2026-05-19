using Application.Common;
using Domain.Tickets;
using MediatR;

namespace Application.Tickets;

public sealed record UpdateTicketCommand(
    Guid Id,
    string? CustomerEmail = null,
    string? CustomerName = null,
    string? Subject = null,
    string? Description = null,
    TicketCategory? Category = null,
    TicketPriority? Priority = null,
    TicketStatus? Status = null,
    IReadOnlyCollection<string>? Tags = null,
    TicketMetadata? Metadata = null,
    string? AssignedTo = null,
    ManualClassification? Classification = null,
    bool AutoClassify = false) : IRequest<ApplicationResult<TicketDto>>;

public sealed record ManualClassification(
    TicketCategory Category,
    TicketPriority Priority,
    double Confidence,
    string? Reasoning,
    IReadOnlyCollection<string>? KeywordsFound);
