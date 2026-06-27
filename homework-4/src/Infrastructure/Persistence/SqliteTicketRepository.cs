using System.Globalization;
using System.Text.Json;
using Application.Tickets;
using Dapper;
using Domain.Tickets;
using Infrastructure.Notifications;
using Microsoft.Data.Sqlite;

namespace Infrastructure.Persistence;

public sealed class SqliteTicketRepository(
    ISqliteConnectionFactory connectionFactory,
    ITelegramNotifier telegramNotifier) : ITicketRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<T> Execute<T>(Func<Task<T>> operation, string action, CancellationToken cancellationToken)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            await telegramNotifier.NotifyError(
                $"Database operation '{action}' failed: {ex.Message}",
                cancellationToken);
            throw;
        }
    }

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnection(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            CREATE TABLE IF NOT EXISTS tickets (
                id TEXT PRIMARY KEY,
                customer_id TEXT NOT NULL,
                customer_email TEXT NOT NULL,
                customer_name TEXT NOT NULL,
                subject TEXT NOT NULL,
                description TEXT NOT NULL,
                category TEXT NOT NULL,
                priority TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                resolved_at TEXT NULL,
                assigned_to TEXT NULL,
                tags_json TEXT NOT NULL,
                metadata_json TEXT NOT NULL,
                classification_confidence REAL NULL,
                classification_reasoning TEXT NULL,
                classification_keywords_json TEXT NOT NULL DEFAULT '[]'
            );

            CREATE INDEX IF NOT EXISTS ix_tickets_category ON tickets(category);
            CREATE INDEX IF NOT EXISTS ix_tickets_priority ON tickets(priority);
            CREATE INDEX IF NOT EXISTS ix_tickets_status ON tickets(status);
            """,
            cancellationToken: cancellationToken));

        await EnsureClassificationColumns(connection, cancellationToken);
    }

    public async Task Add(Ticket ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        await Execute(async () =>
        {
            await using var connection = await connectionFactory.OpenConnection(cancellationToken);
            return await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO tickets (
                id,
                customer_id,
                customer_email,
                customer_name,
                subject,
                description,
                category,
                priority,
                status,
                created_at,
                updated_at,
                resolved_at,
                assigned_to,
                tags_json,
                metadata_json,
                classification_confidence,
                classification_reasoning,
                classification_keywords_json
            )
            VALUES (
                @Id,
                @CustomerId,
                @CustomerEmail,
                @CustomerName,
                @Subject,
                @Description,
                @Category,
                @Priority,
                @Status,
                @CreatedAt,
                @UpdatedAt,
                @ResolvedAt,
                @AssignedTo,
                @TagsJson,
                @MetadataJson,
                @ClassificationConfidence,
                @ClassificationReasoning,
                @ClassificationKeywordsJson
            );
            """,
            ToParameters(ticket),
            cancellationToken: cancellationToken));
        }, "Add", cancellationToken);
    }

    public async Task<Ticket?> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await Execute(async () =>
        {
            await using var connection = await connectionFactory.OpenConnection(cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<TicketRow>(new CommandDefinition(
            """
            SELECT
                id,
                customer_id AS CustomerId,
                customer_email AS CustomerEmail,
                customer_name AS CustomerName,
                subject,
                description,
                category,
                priority,
                status,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                resolved_at AS ResolvedAt,
                assigned_to AS AssignedTo,
                tags_json AS TagsJson,
                metadata_json AS MetadataJson,
                classification_confidence AS ClassificationConfidence,
                classification_reasoning AS ClassificationReasoning,
                classification_keywords_json AS ClassificationKeywordsJson
            FROM tickets
            WHERE id = @Id;
            """,
            new { Id = id.ToString() },
            cancellationToken: cancellationToken));
        }, "GetById", cancellationToken);

        return row is null ? null : ToTicket(row);
    }

    public async Task<IReadOnlyList<Ticket>> List(TicketFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var category = filter.Category?.ToString();
        var priority = filter.Priority?.ToString();
        var status = filter.Status?.ToString();

        var rows = await Execute(async () =>
        {
            await using var connection = await connectionFactory.OpenConnection(cancellationToken);
            return await connection.QueryAsync<TicketRow>(new CommandDefinition(
            """
            SELECT
                id,
                customer_id AS CustomerId,
                customer_email AS CustomerEmail,
                customer_name AS CustomerName,
                subject,
                description,
                category,
                priority,
                status,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                resolved_at AS ResolvedAt,
                assigned_to AS AssignedTo,
                tags_json AS TagsJson,
                metadata_json AS MetadataJson,
                classification_confidence AS ClassificationConfidence,
                classification_reasoning AS ClassificationReasoning,
                classification_keywords_json AS ClassificationKeywordsJson
            FROM tickets
            WHERE (@Category IS NULL OR category = @Category)
              AND (@Priority IS NULL OR priority = @Priority)
              AND (@Status IS NULL OR status = @Status)
            ORDER BY created_at ASC;
            """,
            new { Category = category, Priority = priority, Status = status },
            cancellationToken: cancellationToken));
        }, "List", cancellationToken);

        return rows.Select(ToTicket).ToArray();
    }

    public async Task<bool> Update(Ticket ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var affectedRows = await Execute(async () =>
        {
            await using var connection = await connectionFactory.OpenConnection(cancellationToken);
            return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE tickets
            SET
                customer_id = @CustomerId,
                customer_email = @CustomerEmail,
                customer_name = @CustomerName,
                subject = @Subject,
                description = @Description,
                category = @Category,
                priority = @Priority,
                status = @Status,
                created_at = @CreatedAt,
                updated_at = @UpdatedAt,
                resolved_at = @ResolvedAt,
                assigned_to = @AssignedTo,
                tags_json = @TagsJson,
                metadata_json = @MetadataJson,
                classification_confidence = @ClassificationConfidence,
                classification_reasoning = @ClassificationReasoning,
                classification_keywords_json = @ClassificationKeywordsJson
            WHERE id = @Id;
            """,
            ToParameters(ticket),
            cancellationToken: cancellationToken));
        }, "Update", cancellationToken);

        return affectedRows > 0;
    }

    public async Task<bool> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var affectedRows = await Execute(async () =>
        {
            await using var connection = await connectionFactory.OpenConnection(cancellationToken);
            return await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM tickets WHERE id = @Id;",
                new { Id = id.ToString() },
                cancellationToken: cancellationToken));
        }, "Delete", cancellationToken);

        return affectedRows > 0;
    }

    private static object ToParameters(Ticket ticket)
    {
        return new
        {
            Id = ticket.Id.ToString(),
            CustomerId = ticket.CustomerId,
            CustomerEmail = ticket.CustomerEmail,
            CustomerName = ticket.CustomerName,
            Subject = ticket.Subject,
            Description = ticket.Description,
            Category = ticket.Category.ToString(),
            Priority = ticket.Priority.ToString(),
            Status = ticket.Status.ToString(),
            CreatedAt = Format(ticket.CreatedAt),
            UpdatedAt = Format(ticket.UpdatedAt),
            ResolvedAt = ticket.ResolvedAt is null ? null : Format(ticket.ResolvedAt.Value),
            AssignedTo = ticket.AssignedTo,
            TagsJson = JsonSerializer.Serialize(ticket.Tags, JsonOptions),
            MetadataJson = JsonSerializer.Serialize(
                new StoredMetadata(
                    ticket.Metadata.Source!.Value.ToString(),
                    ticket.Metadata.Browser,
                    ticket.Metadata.DeviceType!.Value.ToString()),
                JsonOptions),
            ClassificationConfidence = ticket.Classification?.Confidence,
            ClassificationReasoning = ticket.Classification?.Reasoning,
            ClassificationKeywordsJson = JsonSerializer.Serialize(ticket.Classification?.KeywordsFound ?? [], JsonOptions),
        };
    }

    private static async Task EnsureClassificationColumns(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var columns = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT name FROM pragma_table_info('tickets');",
            cancellationToken: cancellationToken))).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!columns.Contains("classification_confidence"))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE tickets ADD COLUMN classification_confidence REAL NULL;",
                cancellationToken: cancellationToken));
        }

        if (!columns.Contains("classification_reasoning"))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE tickets ADD COLUMN classification_reasoning TEXT NULL;",
                cancellationToken: cancellationToken));
        }

        if (!columns.Contains("classification_keywords_json"))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "ALTER TABLE tickets ADD COLUMN classification_keywords_json TEXT NOT NULL DEFAULT '[]';",
                cancellationToken: cancellationToken));
        }
    }

    private static Ticket ToTicket(TicketRow row)
    {
        var draft = new TicketDraft(
            row.CustomerId,
            row.CustomerEmail,
            row.CustomerName,
            row.Subject,
            row.Description,
            ParseEnum<TicketCategory>(row.Category),
            ParseEnum<TicketPriority>(row.Priority),
            ParseEnum<TicketStatus>(row.Status),
            DeserializeTags(row.TagsJson),
            DeserializeMetadata(row.MetadataJson),
            row.AssignedTo,
            ToClassification(row));

        var result = Ticket.Rehydrate(
            Guid.Parse(row.Id),
            draft,
            ParseDateTimeOffset(row.CreatedAt),
            ParseDateTimeOffset(row.UpdatedAt),
            row.ResolvedAt is null ? null : ParseDateTimeOffset(row.ResolvedAt));

        if (result.IsValid)
        {
            return result.Value!;
        }

        var errors = string.Join("; ", result.Errors.Select(error => $"{error.Field}: {error.Message}"));
        throw new InvalidOperationException($"Stored ticket row is invalid: {errors}");
    }

    private static IReadOnlyCollection<string> DeserializeTags(string tagsJson)
    {
        return JsonSerializer.Deserialize<string[]>(tagsJson, JsonOptions) ?? [];
    }

    private static TicketMetadata DeserializeMetadata(string metadataJson)
    {
        var metadata = JsonSerializer.Deserialize<StoredMetadata>(metadataJson, JsonOptions)
            ?? throw new InvalidOperationException("Stored ticket metadata is invalid.");

        return new TicketMetadata(
            ParseEnum<TicketSource>(metadata.Source),
            metadata.Browser,
            ParseEnum<DeviceType>(metadata.DeviceType));
    }

    private static TicketClassification? ToClassification(TicketRow row)
    {
        if (!row.ClassificationConfidence.HasValue || string.IsNullOrWhiteSpace(row.ClassificationReasoning))
        {
            return null;
        }

        return new TicketClassification(
            ParseEnum<TicketCategory>(row.Category),
            ParseEnum<TicketPriority>(row.Priority),
            row.ClassificationConfidence.Value,
            row.ClassificationReasoning,
            DeserializeTags(row.ClassificationKeywordsJson).ToArray());
    }

    private static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct, Enum
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidOperationException($"Stored value '{value}' is not a valid {typeof(TEnum).Name}.");
    }

    private static string Format(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseDateTimeOffset(string value)
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private sealed record StoredMetadata(string Source, string? Browser, string DeviceType);

    private sealed class TicketRow
    {
        public required string Id { get; init; }

        public required string CustomerId { get; init; }

        public required string CustomerEmail { get; init; }

        public required string CustomerName { get; init; }

        public required string Subject { get; init; }

        public required string Description { get; init; }

        public required string Category { get; init; }

        public required string Priority { get; init; }

        public required string Status { get; init; }

        public required string CreatedAt { get; init; }

        public required string UpdatedAt { get; init; }

        public string? ResolvedAt { get; init; }

        public string? AssignedTo { get; init; }

        public required string TagsJson { get; init; }

        public required string MetadataJson { get; init; }

        public double? ClassificationConfidence { get; init; }

        public string? ClassificationReasoning { get; init; }

        public required string ClassificationKeywordsJson { get; init; }
    }
}
