using Application.Common;
using Application.Tickets;
using Domain.Tickets;

namespace Tests;

public sealed class ParameterizedQueryTests
{
    private readonly InMemoryTicketRepository _repository = new();

    [Fact]
    public async Task Add_WithSingleQuoteInDescription_StoresVerbatimAndRetrievesExactly()
    {
        // Arrange
        var ticket = CreateTicketWithDescription("Issue: O'Brien's account locked");

        // Act
        await _repository.Add(ticket);
        var retrieved = await _repository.GetById(ticket.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Issue: O'Brien's account locked", retrieved.Description);
    }

    [Fact]
    public async Task Add_WithSqlLikePayloadInDescription_StoresVerbatim()
    {
        // Arrange
        var payload = "'); DROP TABLE tickets; --";
        var ticket = CreateTicketWithDescription(payload);

        // Act
        await _repository.Add(ticket);
        var retrieved = await _repository.GetById(ticket.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(payload, retrieved.Description);
    }

    [Fact]
    public async Task GetById_WithValidId_ReturnsTicket()
    {
        // Arrange
        var ticket = CreateTicket();
        await _repository.Add(ticket);

        // Act
        var result = await _repository.GetById(ticket.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ticket.Id, result.Id);
    }

    [Fact]
    public async Task List_WithNullCategoryFilter_ReturnsAllTickets()
    {
        // Arrange
        var ticket1 = CreateTicket();
        var ticket2 = CreateTicket();
        await _repository.Add(ticket1);
        await _repository.Add(ticket2);
        var filter = new TicketFilter { Category = null, Priority = null, Status = null };

        // Act
        var results = await _repository.List(filter);

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task List_WithCategoryFilter_ReturnsOnlyMatching()
    {
        // Arrange
        var billing = CreateTicketWithCategory(TicketCategory.BillingQuestion);
        var technical = CreateTicketWithCategory(TicketCategory.TechnicalIssue);
        await _repository.Add(billing);
        await _repository.Add(technical);
        var filter = new TicketFilter { Category = TicketCategory.BillingQuestion, Priority = null, Status = null };

        // Act
        var results = await _repository.List(filter);

        // Assert
        Assert.Single(results);
        Assert.Equal(billing.Id, results[0].Id);
    }

    [Fact]
    public async Task List_WithMultipleFilters_ReturnsOnlyMatching()
    {
        // Arrange
        var match = CreateTicketWithCategoryAndPriority(TicketCategory.BillingQuestion, TicketPriority.High);
        var other1 = CreateTicketWithCategory(TicketCategory.TechnicalIssue);
        var other2 = CreateTicketWithCategoryAndPriority(TicketCategory.BillingQuestion, TicketPriority.Low);
        await _repository.Add(match);
        await _repository.Add(other1);
        await _repository.Add(other2);
        var filter = new TicketFilter { Category = TicketCategory.BillingQuestion, Priority = TicketPriority.High, Status = null };

        // Act
        var results = await _repository.List(filter);

        // Assert
        Assert.Single(results);
        Assert.Equal(match.Id, results[0].Id);
    }

    [Fact]
    public async Task Update_WithSingleQuoteInDescription_StoresAndRetrievesVerbatim()
    {
        // Arrange
        var ticket = CreateTicket();
        await _repository.Add(ticket);
        var updated = Ticket.Rehydrate(
            ticket.Id,
            new TicketDraft(
                ticket.CustomerId,
                ticket.CustomerEmail,
                ticket.CustomerName,
                ticket.Subject,
                "Customer: O'Malley's complaint",
                ticket.Category,
                ticket.Priority,
                ticket.Status,
                ticket.Tags,
                ticket.Metadata),
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.ResolvedAt).Value!;

        // Act
        var success = await _repository.Update(updated);
        var retrieved = await _repository.GetById(ticket.Id);

        // Assert
        Assert.True(success);
        Assert.NotNull(retrieved);
        Assert.Equal("Customer: O'Malley's complaint", retrieved.Description);
    }

    [Fact]
    public async Task Delete_WithValidId_RemovesRecord()
    {
        // Arrange
        var ticket = CreateTicket();
        await _repository.Add(ticket);

        // Act
        var success = await _repository.Delete(ticket.Id);
        var retrieved = await _repository.GetById(ticket.Id);

        // Assert
        Assert.True(success);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task Delete_WithNonexistentId_ReturnsFalse()
    {
        // Act
        var success = await _repository.Delete(Guid.NewGuid());

        // Assert
        Assert.False(success);
    }

    [Fact]
    public async Task Add_WithSpecialCharacterInCustomerId_StoresVerbatim()
    {
        // Arrange
        var draft = new TicketDraft(
            CustomerId: "cust-1' OR '1'='1",
            CustomerEmail: "test@example.com",
            CustomerName: "Test User",
            Subject: "Test Subject",
            Description: "Test Description",
            Category: TicketCategory.Other,
            Priority: TicketPriority.Medium,
            Status: TicketStatus.New,
            Tags: ["test"],
            Metadata: new TicketMetadata(TicketSource.WebForm, "Chrome", DeviceType.Desktop));
        var result = Ticket.Create(draft, new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero));
        var ticket = result.Value!;

        // Act
        await _repository.Add(ticket);
        var retrieved = await _repository.GetById(ticket.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("cust-1' OR '1'='1", retrieved.CustomerId);
    }

    [Fact]
    public async Task Add_WithSpecialCharacterInCustomerName_StoresVerbatim()
    {
        // Arrange
        var draft = new TicketDraft(
            CustomerId: "customer-1",
            CustomerEmail: "test@example.com",
            CustomerName: "O'Neill-Smith",
            Subject: "Test Subject",
            Description: "Test Description",
            Category: TicketCategory.Other,
            Priority: TicketPriority.Medium,
            Status: TicketStatus.New,
            Tags: ["test"],
            Metadata: new TicketMetadata(TicketSource.WebForm, "Chrome", DeviceType.Desktop));
        var result = Ticket.Create(draft, new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero));
        var ticket = result.Value!;

        // Act
        await _repository.Add(ticket);
        var retrieved = await _repository.GetById(ticket.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("O'Neill-Smith", retrieved.CustomerName);
    }

    private static Ticket CreateTicket()
    {
        var draft = new TicketDraft(
            CustomerId: "customer-1",
            CustomerEmail: "test@example.com",
            CustomerName: "Test User",
            Subject: "Test Subject",
            Description: "Test Description",
            Category: TicketCategory.Other,
            Priority: TicketPriority.Medium,
            Status: TicketStatus.New,
            Tags: ["test"],
            Metadata: new TicketMetadata(TicketSource.WebForm, "Chrome", DeviceType.Desktop));
        var result = Ticket.Create(draft, new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero));
        return result.Value!;
    }

    private static Ticket CreateTicketWithDescription(string description)
    {
        var draft = new TicketDraft(
            CustomerId: "customer-1",
            CustomerEmail: "test@example.com",
            CustomerName: "Test User",
            Subject: "Test Subject",
            Description: description,
            Category: TicketCategory.Other,
            Priority: TicketPriority.Medium,
            Status: TicketStatus.New,
            Tags: ["test"],
            Metadata: new TicketMetadata(TicketSource.WebForm, "Chrome", DeviceType.Desktop));
        var result = Ticket.Create(draft, new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero));
        return result.Value!;
    }

    private static Ticket CreateTicketWithCategory(TicketCategory category)
    {
        var draft = new TicketDraft(
            CustomerId: "customer-1",
            CustomerEmail: "test@example.com",
            CustomerName: "Test User",
            Subject: "Test Subject",
            Description: "Test Description",
            Category: category,
            Priority: TicketPriority.Medium,
            Status: TicketStatus.New,
            Tags: ["test"],
            Metadata: new TicketMetadata(TicketSource.WebForm, "Chrome", DeviceType.Desktop));
        var result = Ticket.Create(draft, new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero));
        return result.Value!;
    }

    private static Ticket CreateTicketWithCategoryAndPriority(TicketCategory category, TicketPriority priority)
    {
        var draft = new TicketDraft(
            CustomerId: "customer-1",
            CustomerEmail: "test@example.com",
            CustomerName: "Test User",
            Subject: "Test Subject",
            Description: "Test Description",
            Category: category,
            Priority: priority,
            Status: TicketStatus.New,
            Tags: ["test"],
            Metadata: new TicketMetadata(TicketSource.WebForm, "Chrome", DeviceType.Desktop));
        var result = Ticket.Create(draft, new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero));
        return result.Value!;
    }

    private sealed class InMemoryTicketRepository : ITicketRepository
    {
        private readonly List<Ticket> _tickets = [];

        public Task Initialize(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Add(Ticket ticket, CancellationToken cancellationToken = default)
        {
            _tickets.Add(ticket);
            return Task.CompletedTask;
        }

        public Task<Ticket?> GetById(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tickets.SingleOrDefault(ticket => ticket.Id == id));
        }

        public Task<IReadOnlyList<Ticket>> List(TicketFilter filter, CancellationToken cancellationToken = default)
        {
            var result = _tickets.AsEnumerable();

            if (filter.Category.HasValue)
                result = result.Where(t => t.Category == filter.Category);
            if (filter.Priority.HasValue)
                result = result.Where(t => t.Priority == filter.Priority);
            if (filter.Status.HasValue)
                result = result.Where(t => t.Status == filter.Status);

            return Task.FromResult<IReadOnlyList<Ticket>>(result.ToArray());
        }

        public Task<bool> Update(Ticket ticket, CancellationToken cancellationToken = default)
        {
            var index = _tickets.FindIndex(stored => stored.Id == ticket.Id);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            _tickets[index] = ticket;
            return Task.FromResult(true);
        }

        public Task<bool> Delete(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tickets.RemoveAll(ticket => ticket.Id == id) > 0);
        }
    }
}
