using Application.Common;
using Domain.Tickets;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Tickets;

public sealed class CreateTicketCommandHandler(
    ITicketRepository repository,
    IValidator<CreateTicketCommand> validator,
    IClock clock,
    ITicketClassifier? classifier = null,
    ILogger<CreateTicketCommandHandler>? logger = null) : IRequestHandler<CreateTicketCommand, ApplicationResult<TicketDto>>
{
    public async Task<ApplicationResult<TicketDto>> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ApplicationResult<TicketDto>.ValidationFailure(ErrorMapper.FromValidationFailures(validation.Errors));
        }

        var result = Ticket.Create(ToDraft(request), clock.UtcNow);
        if (!result.IsValid)
        {
            return ApplicationResult<TicketDto>.ValidationFailure(ErrorMapper.FromDomainErrors(result.Errors));
        }

        var ticket = result.Value!;
        if (request.AutoClassify)
        {
            if (classifier is null)
            {
                return ApplicationResult<TicketDto>.ValidationFailure([new ApplicationError(nameof(request.AutoClassify), "Ticket classifier is not configured.")]);
            }

            var classification = classifier.Classify(ticket);
            ticket = Reclassify(ticket, classification, clock.UtcNow);
            logger?.LogInformation(
                "Auto-classified ticket {TicketId} as {Category}/{Priority} with confidence {Confidence}.",
                ticket.Id,
                classification.Category,
                classification.Priority,
                classification.Confidence);
        }

        await repository.Add(ticket, cancellationToken);
        return ApplicationResult<TicketDto>.Success(TicketMapper.ToDto(ticket));
    }

    private static TicketDraft ToDraft(CreateTicketCommand command)
    {
        return new TicketDraft(
            command.CustomerId,
            command.CustomerEmail,
            command.CustomerName,
            command.Subject,
            command.Description,
            command.Category,
            command.Priority,
            command.Status,
            command.Tags,
            command.Metadata,
            command.AssignedTo);
    }

    private static Ticket Reclassify(Ticket ticket, TicketClassification classification, DateTimeOffset timestamp)
    {
        var result = Ticket.Rehydrate(
            ticket.Id,
            new TicketDraft(
                ticket.CustomerId,
                ticket.CustomerEmail,
                ticket.CustomerName,
                ticket.Subject,
                ticket.Description,
                classification.Category,
                classification.Priority,
                ticket.Status,
                ticket.Tags,
                ticket.Metadata,
                ticket.AssignedTo,
                classification),
            ticket.CreatedAt,
            timestamp,
            ticket.ResolvedAt);

        return result.Value!;
    }
}

public sealed class GetTicketQueryHandler(
    ITicketRepository repository,
    IValidator<GetTicketQuery> validator) : IRequestHandler<GetTicketQuery, ApplicationResult<TicketDto>>
{
    public async Task<ApplicationResult<TicketDto>> Handle(GetTicketQuery request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ApplicationResult<TicketDto>.ValidationFailure(ErrorMapper.FromValidationFailures(validation.Errors));
        }

        var ticket = await repository.GetById(request.Id, cancellationToken);

        return ticket is null
            ? ApplicationResult<TicketDto>.NotFound(nameof(request.Id), "Ticket was not found.")
            : ApplicationResult<TicketDto>.Success(TicketMapper.ToDto(ticket));
    }
}

public sealed class ListTicketsQueryHandler(
    ITicketRepository repository,
    IValidator<ListTicketsQuery> validator) : IRequestHandler<ListTicketsQuery, ApplicationResult<IReadOnlyList<TicketDto>>>
{
    public async Task<ApplicationResult<IReadOnlyList<TicketDto>>> Handle(ListTicketsQuery request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ApplicationResult<IReadOnlyList<TicketDto>>.ValidationFailure(ErrorMapper.FromValidationFailures(validation.Errors));
        }

        var tickets = await repository.List(new TicketFilter(request.Category, request.Priority, request.Status), cancellationToken);
        var ticketDtos = tickets.Select(TicketMapper.ToDto).ToArray();

        return ApplicationResult<IReadOnlyList<TicketDto>>.Success(ticketDtos);
    }
}

public sealed class UpdateTicketCommandHandler(
    ITicketRepository repository,
    IValidator<UpdateTicketCommand> validator,
    IClock clock,
    ITicketClassifier? classifier = null,
    ILogger<UpdateTicketCommandHandler>? logger = null) : IRequestHandler<UpdateTicketCommand, ApplicationResult<TicketDto>>
{
    public async Task<ApplicationResult<TicketDto>> Handle(UpdateTicketCommand request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ApplicationResult<TicketDto>.ValidationFailure(ErrorMapper.FromValidationFailures(validation.Errors));
        }

        var existing = await repository.GetById(request.Id, cancellationToken);
        if (existing is null)
        {
            return ApplicationResult<TicketDto>.NotFound(nameof(request.Id), "Ticket was not found.");
        }

        var now = clock.UtcNow;
        var updatedStatus = request.Status ?? existing.Status;
        var resolvedAt = ResolveTimestamp(existing, request.Status, now);
        var result = Ticket.Rehydrate(
            existing.Id,
            ToDraft(existing, request, updatedStatus),
            existing.CreatedAt,
            now,
            resolvedAt);

        if (!result.IsValid)
        {
            return ApplicationResult<TicketDto>.ValidationFailure(ErrorMapper.FromDomainErrors(result.Errors));
        }

        var ticket = result.Value!;
        if (request.AutoClassify)
        {
            if (classifier is null)
            {
                return ApplicationResult<TicketDto>.ValidationFailure([new ApplicationError(nameof(request.AutoClassify), "Ticket classifier is not configured.")]);
            }

            var classification = classifier.Classify(ticket);
            ticket = Reclassify(ticket, classification, now);
            logger?.LogInformation(
                "Auto-classified ticket {TicketId} as {Category}/{Priority} with confidence {Confidence}.",
                ticket.Id,
                classification.Category,
                classification.Priority,
                classification.Confidence);
        }

        var updated = await repository.Update(ticket, cancellationToken);
        if (!updated)
        {
            return ApplicationResult<TicketDto>.NotFound(nameof(request.Id), "Ticket was not found.");
        }

        var manuallyOverwrittenFields = GetManuallyOverwrittenClassificationFields(request);
        if (manuallyOverwrittenFields.Length > 0)
        {
            logger?.LogInformation(
                "Manually overwrote classification fields for ticket {TicketId}: {UpdatedFields}.",
                request.Id,
                string.Join(", ", manuallyOverwrittenFields));
        }

        return ApplicationResult<TicketDto>.Success(TicketMapper.ToDto(ticket));
    }

    private static TicketDraft ToDraft(Ticket existing, UpdateTicketCommand command, TicketStatus status)
    {
        var classification = ToClassification(existing, command);

        return new TicketDraft(
            existing.CustomerId,
            command.CustomerEmail ?? existing.CustomerEmail,
            command.CustomerName ?? existing.CustomerName,
            command.Subject ?? existing.Subject,
            command.Description ?? existing.Description,
            command.Classification?.Category ?? command.Category ?? existing.Category,
            command.Classification?.Priority ?? command.Priority ?? existing.Priority,
            status,
            command.Tags ?? existing.Tags,
            command.Metadata ?? existing.Metadata,
            command.AssignedTo ?? existing.AssignedTo,
            classification);
    }

    private static TicketClassification? ToClassification(Ticket existing, UpdateTicketCommand command)
    {
        if (command.AutoClassify)
        {
            return null;
        }

        if (command.Classification is not null)
        {
            return new TicketClassification(
                command.Classification.Category,
                command.Classification.Priority,
                command.Classification.Confidence,
                string.IsNullOrWhiteSpace(command.Classification.Reasoning) ? "specified manually" : command.Classification.Reasoning,
                command.Classification.KeywordsFound?.ToArray() ?? []);
        }

        return command.Category.HasValue || command.Priority.HasValue
            ? null
            : existing.Classification;
    }

    private static string[] GetManuallyOverwrittenClassificationFields(UpdateTicketCommand command)
    {
        var fields = new List<string>();
        if (command.Category.HasValue)
        {
            fields.Add("category");
        }

        if (command.Priority.HasValue)
        {
            fields.Add("priority");
        }

        if (command.Classification is not null)
        {
            fields.Add("classification");
        }

        return fields.ToArray();
    }

    private static Ticket Reclassify(Ticket ticket, TicketClassification classification, DateTimeOffset timestamp)
    {
        var result = Ticket.Rehydrate(
            ticket.Id,
            new TicketDraft(
                ticket.CustomerId,
                ticket.CustomerEmail,
                ticket.CustomerName,
                ticket.Subject,
                ticket.Description,
                classification.Category,
                classification.Priority,
                ticket.Status,
                ticket.Tags,
                ticket.Metadata,
                ticket.AssignedTo,
                classification),
            ticket.CreatedAt,
            timestamp,
            ticket.ResolvedAt);

        return result.Value!;
    }

    private static DateTimeOffset? ResolveTimestamp(Ticket existing, TicketStatus? requestedStatus, DateTimeOffset now)
    {
        if (!requestedStatus.HasValue)
        {
            return existing.ResolvedAt;
        }

        return requestedStatus.Value is TicketStatus.Resolved or TicketStatus.Closed
            ? existing.ResolvedAt ?? now
            : null;
    }
}

public sealed class DeleteTicketCommandHandler(
    ITicketRepository repository,
    IValidator<DeleteTicketCommand> validator) : IRequestHandler<DeleteTicketCommand, ApplicationResult<DeleteTicketResponse>>
{
    public async Task<ApplicationResult<DeleteTicketResponse>> Handle(DeleteTicketCommand request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ApplicationResult<DeleteTicketResponse>.ValidationFailure(ErrorMapper.FromValidationFailures(validation.Errors));
        }

        var deleted = await repository.Delete(request.Id, cancellationToken);

        return deleted
            ? ApplicationResult<DeleteTicketResponse>.Success(new DeleteTicketResponse(request.Id))
            : ApplicationResult<DeleteTicketResponse>.NotFound(nameof(request.Id), "Ticket was not found.");
    }
}

public sealed class AutoClassifyTicketCommandHandler(
    ITicketRepository repository,
    IValidator<AutoClassifyTicketCommand> validator,
    ITicketClassifier classifier,
    IClock clock,
    ILogger<AutoClassifyTicketCommandHandler> logger) : IRequestHandler<AutoClassifyTicketCommand, ApplicationResult<ClassificationDto>>
{
    public async Task<ApplicationResult<ClassificationDto>> Handle(AutoClassifyTicketCommand request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ApplicationResult<ClassificationDto>.ValidationFailure(ErrorMapper.FromValidationFailures(validation.Errors));
        }

        var existing = await repository.GetById(request.Id, cancellationToken);
        if (existing is null)
        {
            return ApplicationResult<ClassificationDto>.NotFound(nameof(request.Id), "Ticket was not found.");
        }

        var classification = classifier.Classify(existing);
        var result = Ticket.Rehydrate(
            existing.Id,
            new TicketDraft(
                existing.CustomerId,
                existing.CustomerEmail,
                existing.CustomerName,
                existing.Subject,
                existing.Description,
                classification.Category,
                classification.Priority,
                existing.Status,
                existing.Tags,
                existing.Metadata,
                existing.AssignedTo,
                classification),
            existing.CreatedAt,
            clock.UtcNow,
            existing.ResolvedAt);

        if (!result.IsValid)
        {
            return ApplicationResult<ClassificationDto>.ValidationFailure(ErrorMapper.FromDomainErrors(result.Errors));
        }

        var updated = await repository.Update(result.Value!, cancellationToken);
        if (!updated)
        {
            return ApplicationResult<ClassificationDto>.NotFound(nameof(request.Id), "Ticket was not found.");
        }

        logger.LogInformation(
            "Auto-classified ticket {TicketId} as {Category}/{Priority} with confidence {Confidence}.",
            existing.Id,
            classification.Category,
            classification.Priority,
            classification.Confidence);

        return ApplicationResult<ClassificationDto>.Success(TicketMapper.ToDto(classification));
    }
}
