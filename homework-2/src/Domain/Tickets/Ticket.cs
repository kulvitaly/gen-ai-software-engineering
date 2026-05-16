using System.Net.Mail;

namespace Domain.Tickets;

public sealed class Ticket
{
    private Ticket(
        Guid id,
        string customerId,
        string customerEmail,
        string customerName,
        string subject,
        string description,
        TicketCategory category,
        TicketPriority priority,
        TicketStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? resolvedAt,
        string? assignedTo,
        IReadOnlyList<string> tags,
        TicketMetadata metadata)
    {
        Id = id;
        CustomerId = customerId;
        CustomerEmail = customerEmail;
        CustomerName = customerName;
        Subject = subject;
        Description = description;
        Category = category;
        Priority = priority;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ResolvedAt = resolvedAt;
        AssignedTo = assignedTo;
        Tags = tags;
        Metadata = metadata;
    }

    public Guid Id { get; }

    public string CustomerId { get; }

    public string CustomerEmail { get; }

    public string CustomerName { get; }

    public string Subject { get; }

    public string Description { get; }

    public TicketCategory Category { get; }

    public TicketPriority Priority { get; }

    public TicketStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public string? AssignedTo { get; }

    public IReadOnlyList<string> Tags { get; }

    public TicketMetadata Metadata { get; }

    public static ValidationResult<Ticket> Create(TicketDraft draft, DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = Validate(draft);
        if (errors.Count > 0)
        {
            return ValidationResult<Ticket>.Failure(errors);
        }

        var now = timestamp ?? DateTimeOffset.UtcNow;
        var ticket = new Ticket(
            Guid.NewGuid(),
            draft.CustomerId!.Trim(),
            draft.CustomerEmail!.Trim(),
            draft.CustomerName!.Trim(),
            draft.Subject!.Trim(),
            draft.Description!.Trim(),
            draft.Category!.Value,
            draft.Priority!.Value,
            draft.Status!.Value,
            now,
            now,
            null,
            NormalizeOptional(draft.AssignedTo),
            draft.Tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).ToArray() ?? [],
            draft.Metadata!);

        return ValidationResult<Ticket>.Success(ticket);
    }

    public static ValidationResult<Ticket> Rehydrate(
        Guid id,
        TicketDraft draft,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset? resolvedAt)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = Validate(draft);
        if (id == Guid.Empty)
        {
            errors.Add(new ValidationError(nameof(Id), "Ticket id is required."));
        }

        if (updatedAt < createdAt)
        {
            errors.Add(new ValidationError(nameof(UpdatedAt), "UpdatedAt cannot be earlier than CreatedAt."));
        }

        if (resolvedAt.HasValue && resolvedAt.Value < createdAt)
        {
            errors.Add(new ValidationError(nameof(ResolvedAt), "ResolvedAt cannot be earlier than CreatedAt."));
        }

        if (errors.Count > 0)
        {
            return ValidationResult<Ticket>.Failure(errors);
        }

        var ticket = new Ticket(
            id,
            draft.CustomerId!.Trim(),
            draft.CustomerEmail!.Trim(),
            draft.CustomerName!.Trim(),
            draft.Subject!.Trim(),
            draft.Description!.Trim(),
            draft.Category!.Value,
            draft.Priority!.Value,
            draft.Status!.Value,
            createdAt,
            updatedAt,
            resolvedAt,
            NormalizeOptional(draft.AssignedTo),
            draft.Tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).ToArray() ?? [],
            draft.Metadata!);

        return ValidationResult<Ticket>.Success(ticket);
    }

    public void ChangeStatus(TicketStatus status, DateTimeOffset? timestamp = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Ticket status is not defined.");
        }

        var now = timestamp ?? DateTimeOffset.UtcNow;
        Status = status;
        UpdatedAt = now;

        if (status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            ResolvedAt ??= now;
        }
        else
        {
            ResolvedAt = null;
        }
    }

    private static List<ValidationError> Validate(TicketDraft draft)
    {
        var errors = new List<ValidationError>();

        Require(draft.CustomerId, nameof(TicketDraft.CustomerId), errors);
        Require(draft.CustomerEmail, nameof(TicketDraft.CustomerEmail), errors);
        Require(draft.CustomerName, nameof(TicketDraft.CustomerName), errors);
        ValidateLength(draft.Subject, nameof(TicketDraft.Subject), 1, 200, errors);
        ValidateLength(draft.Description, nameof(TicketDraft.Description), 10, 2000, errors);
        ValidateEmail(draft.CustomerEmail, errors);
        ValidateEnum(draft.Category, nameof(TicketDraft.Category), errors);
        ValidateEnum(draft.Priority, nameof(TicketDraft.Priority), errors);
        ValidateEnum(draft.Status, nameof(TicketDraft.Status), errors);
        ValidateMetadata(draft.Metadata, errors);

        return errors;
    }

    private static void Require(string? value, string field, ICollection<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError(field, $"{field} is required."));
        }
    }

    private static void ValidateLength(string? value, string field, int minLength, int maxLength, ICollection<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError(field, $"{field} is required."));
            return;
        }

        var trimmedLength = value.Trim().Length;
        if (trimmedLength < minLength || trimmedLength > maxLength)
        {
            errors.Add(new ValidationError(field, $"{field} must be between {minLength} and {maxLength} characters."));
        }
    }

    private static void ValidateEmail(string? email, ICollection<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        try
        {
            var parsed = new MailAddress(email);
            if (!string.Equals(parsed.Address, email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationError(nameof(TicketDraft.CustomerEmail), "CustomerEmail must be a valid email address."));
            }
        }
        catch (FormatException)
        {
            errors.Add(new ValidationError(nameof(TicketDraft.CustomerEmail), "CustomerEmail must be a valid email address."));
        }
    }

    private static void ValidateEnum<TEnum>(TEnum? value, string field, ICollection<ValidationError> errors)
        where TEnum : struct, Enum
    {
        if (!value.HasValue || !Enum.IsDefined(value.Value))
        {
            errors.Add(new ValidationError(field, $"{field} must be a defined value."));
        }
    }

    private static void ValidateMetadata(TicketMetadata? metadata, ICollection<ValidationError> errors)
    {
        if (metadata is null)
        {
            errors.Add(new ValidationError(nameof(TicketDraft.Metadata), "Metadata is required."));
            return;
        }

        if (!metadata.Source.HasValue || !Enum.IsDefined(metadata.Source.Value))
        {
            errors.Add(new ValidationError("Metadata.Source", "Metadata source must be a defined value."));
        }

        if (!metadata.DeviceType.HasValue || !Enum.IsDefined(metadata.DeviceType.Value))
        {
            errors.Add(new ValidationError("Metadata.DeviceType", "Metadata device type must be a defined value."));
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
