using Application.Tickets;
using Domain.Tickets;
using Infrastructure.Notifications;
using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace Tests;

public sealed class TicketRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
    private readonly ITicketRepository _repository;

    public TicketRepositoryTests()
    {
        _repository = new SqliteTicketRepository(
            new SqliteConnectionFactory($"Data Source={_databasePath}"),
            new TelegramNotifier(token: null));
    }

    [Fact]
    public async Task Initialize_CreatesTicketsTableWithJsonColumns()
    {
        // Arrange
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");

        // Act
        await _repository.Initialize();
        var columns = await ReadTicketColumns(connection);

        // Assert
        Assert.Contains("id", columns);
        Assert.Contains("customer_id", columns);
        Assert.Contains("customer_email", columns);
        Assert.Contains("customer_name", columns);
        Assert.Contains("subject", columns);
        Assert.Contains("description", columns);
        Assert.Contains("category", columns);
        Assert.Contains("priority", columns);
        Assert.Contains("status", columns);
        Assert.Contains("created_at", columns);
        Assert.Contains("updated_at", columns);
        Assert.Contains("resolved_at", columns);
        Assert.Contains("assigned_to", columns);
        Assert.Contains("tags_json", columns);
        Assert.Contains("metadata_json", columns);
        Assert.Contains("classification_confidence", columns);
        Assert.Contains("classification_reasoning", columns);
        Assert.Contains("classification_keywords_json", columns);
    }

    [Fact]
    public async Task Add_ThenGetById_RoundTripsAllFields()
    {
        // Arrange
        await _repository.Initialize();
        var ticket = CreateTicket();

        // Act
        await _repository.Add(ticket);
        var stored = await _repository.GetById(ticket.Id);

        // Assert
        Assert.NotNull(stored);
        Assert.Equal(ticket.Id, stored.Id);
        Assert.Equal(ticket.CustomerId, stored.CustomerId);
        Assert.Equal(ticket.CustomerEmail, stored.CustomerEmail);
        Assert.Equal(ticket.CustomerName, stored.CustomerName);
        Assert.Equal(ticket.Subject, stored.Subject);
        Assert.Equal(ticket.Description, stored.Description);
        Assert.Equal(ticket.Category, stored.Category);
        Assert.Equal(ticket.Priority, stored.Priority);
        Assert.Equal(ticket.Status, stored.Status);
        Assert.Equal(ticket.CreatedAt, stored.CreatedAt);
        Assert.Equal(ticket.UpdatedAt, stored.UpdatedAt);
        Assert.Equal(ticket.ResolvedAt, stored.ResolvedAt);
        Assert.Equal(ticket.AssignedTo, stored.AssignedTo);
        Assert.Equal(ticket.Tags, stored.Tags);
        Assert.Equal(ticket.Metadata, stored.Metadata);
        Assert.NotNull(stored.Classification);
        Assert.Equal(ticket.Classification!.Category, stored.Classification.Category);
        Assert.Equal(ticket.Classification.Priority, stored.Classification.Priority);
        Assert.Equal(ticket.Classification.Confidence, stored.Classification.Confidence);
        Assert.Equal(ticket.Classification.Reasoning, stored.Classification.Reasoning);
        Assert.Equal(ticket.Classification.KeywordsFound, stored.Classification.KeywordsFound);
    }

    [Fact]
    public async Task Update_PersistsStatusUpdatedAtAndResolvedAt()
    {
        // Arrange
        await _repository.Initialize();
        var ticket = CreateTicket();
        await _repository.Add(ticket);

        var resolvedAt = ticket.CreatedAt.AddHours(4);
        ticket.ChangeStatus(TicketStatus.Resolved, resolvedAt);

        // Act
        var updated = await _repository.Update(ticket);
        var stored = await _repository.GetById(ticket.Id);

        // Assert
        Assert.True(updated);
        Assert.NotNull(stored);
        Assert.Equal(TicketStatus.Resolved, stored.Status);
        Assert.Equal(resolvedAt, stored.UpdatedAt);
        Assert.Equal(resolvedAt, stored.ResolvedAt);
    }

    [Fact]
    public async Task Delete_RemovesTicket()
    {
        // Arrange
        await _repository.Initialize();
        var ticket = CreateTicket();
        await _repository.Add(ticket);

        // Act
        var deleted = await _repository.Delete(ticket.Id);
        var stored = await _repository.GetById(ticket.Id);

        // Assert
        Assert.True(deleted);
        Assert.Null(stored);
    }

    [Fact]
    public async Task GetById_WhenTicketDoesNotExist_ReturnsNull()
    {
        // Arrange
        await _repository.Initialize();
        var unknownId = Guid.NewGuid();

        // Act
        var ticket = await _repository.GetById(unknownId);

        // Assert
        Assert.Null(ticket);
    }

    [Fact]
    public async Task List_WithCategoryAndPriorityFilter_ReturnsMatchingTickets()
    {
        // Arrange
        await _repository.Initialize();
        var matching = CreateTicket(TicketCategory.BillingQuestion, TicketPriority.High, "matching@example.com");
        var wrongCategory = CreateTicket(TicketCategory.TechnicalIssue, TicketPriority.High, "wrong-category@example.com");
        var wrongPriority = CreateTicket(TicketCategory.BillingQuestion, TicketPriority.Low, "wrong-priority@example.com");
        await _repository.Add(matching);
        await _repository.Add(wrongCategory);
        await _repository.Add(wrongPriority);

        // Act
        var tickets = await _repository.List(new TicketFilter(TicketCategory.BillingQuestion, TicketPriority.High));

        // Assert
        var ticket = Assert.Single(tickets);
        Assert.Equal(matching.Id, ticket.Id);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static async Task<HashSet<string>> ReadTicketColumns(SqliteConnection connection)
    {
        await connection.OpenAsync();

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(tickets);";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(reader.GetOrdinal("name")));
        }

        return columns;
    }

    private static Ticket CreateTicket(
        TicketCategory category = TicketCategory.BillingQuestion,
        TicketPriority priority = TicketPriority.Medium,
        string customerEmail = "grace@example.com")
    {
        var result = Ticket.Create(
            new TicketDraft(
                CustomerId: "customer-42",
                CustomerEmail: customerEmail,
                CustomerName: "Grace Hopper",
                Subject: "Billing invoice question",
                Description: "I need help understanding the latest annual invoice.",
                Category: category,
                Priority: priority,
                Status: TicketStatus.New,
                Tags: ["billing", "invoice"],
                Metadata: new TicketMetadata(TicketSource.Email, "Firefox", DeviceType.Desktop),
                AssignedTo: "agent-7",
                Classification: new TicketClassification(
                    category,
                    priority,
                    0.82,
                    "Matched billing keywords.",
                    ["billing", "invoice"])),
            new DateTimeOffset(2026, 5, 16, 10, 30, 0, TimeSpan.Zero));

        return result.Value!;
    }
}
