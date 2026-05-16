using Application.Tickets;
using Domain.Tickets;
using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace Tests;

public sealed class TicketRepositoryTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
    private readonly ITicketRepository _repository;

    public TicketRepositoryTests()
    {
        _repository = new SqliteTicketRepository(new SqliteConnectionFactory($"Data Source={_databasePath}"));
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

    private static Ticket CreateTicket()
    {
        var result = Ticket.Create(
            new TicketDraft(
                CustomerId: "customer-42",
                CustomerEmail: "grace@example.com",
                CustomerName: "Grace Hopper",
                Subject: "Billing invoice question",
                Description: "I need help understanding the latest annual invoice.",
                Category: TicketCategory.BillingQuestion,
                Priority: TicketPriority.Medium,
                Status: TicketStatus.New,
                Tags: ["billing", "invoice"],
                Metadata: new TicketMetadata(TicketSource.Email, "Firefox", DeviceType.Desktop),
                AssignedTo: "agent-7"),
            new DateTimeOffset(2026, 5, 16, 10, 30, 0, TimeSpan.Zero));

        return result.Value!;
    }
}
