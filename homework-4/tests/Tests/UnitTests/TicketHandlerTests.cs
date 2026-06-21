using Application.Common;
using Application.Tickets;
using Domain.Tickets;

namespace Tests;

public sealed class TicketHandlerTests
{
    private readonly InMemoryTicketRepository _repository = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task CreateTicket_WithInvalidCommand_ReturnsStructuredValidationErrors()
    {
        // Arrange
        var handler = new CreateTicketCommandHandler(_repository, new CreateTicketCommandValidator(), _clock);
        var command = ValidCreateCommand() with
        {
            CustomerEmail = "not-an-email",
            Subject = ""
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ApplicationResultStatus.ValidationError, result.Status);
        Assert.Contains(result.Errors, error => error.Field == nameof(CreateTicketCommand.CustomerEmail));
        Assert.Contains(result.Errors, error => error.Field == nameof(CreateTicketCommand.Subject));
        Assert.Empty(_repository.Tickets);
    }

    [Fact]
    public async Task GetTicket_WhenFound_ReturnsTicket()
    {
        // Arrange
        var ticket = CreateTicket(ValidCreateCommand(), _clock.UtcNow);
        await _repository.Add(ticket);
        var handler = new GetTicketQueryHandler(_repository, new GetTicketQueryValidator());

        // Act
        var result = await handler.Handle(new GetTicketQuery(ticket.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ticket.Id, result.Value.Id);
        Assert.Equal(ticket.Subject, result.Value.Subject);
    }

    [Fact]
    public async Task GetTicket_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var handler = new GetTicketQueryHandler(_repository, new GetTicketQueryValidator());
        var unknownId = Guid.NewGuid();

        // Act
        var result = await handler.Handle(new GetTicketQuery(unknownId), CancellationToken.None);

        // Assert
        Assert.Equal(ApplicationResultStatus.NotFound, result.Status);
        Assert.Contains(result.Errors, error => error.Field == nameof(GetTicketQuery.Id));
    }

    [Fact]
    public async Task ListTickets_WithoutFilter_ReturnsAllTickets()
    {
        // Arrange
        var first = CreateTicket(ValidCreateCommand() with { CustomerEmail = "first@example.com" }, _clock.UtcNow);
        var second = CreateTicket(ValidCreateCommand() with { CustomerEmail = "second@example.com", Category = TicketCategory.TechnicalIssue }, _clock.UtcNow);
        await _repository.Add(first);
        await _repository.Add(second);
        var handler = new ListTicketsQueryHandler(_repository, new ListTicketsQueryValidator());

        // Act
        var result = await handler.Handle(new ListTicketsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task ListTickets_WithCategoryAndPriority_ReturnsMatchingTickets()
    {
        // Arrange
        var matching = CreateTicket(ValidCreateCommand() with { Category = TicketCategory.BillingQuestion, Priority = TicketPriority.High }, _clock.UtcNow);
        var wrongCategory = CreateTicket(ValidCreateCommand() with { Category = TicketCategory.TechnicalIssue, Priority = TicketPriority.High }, _clock.UtcNow);
        var wrongPriority = CreateTicket(ValidCreateCommand() with { Category = TicketCategory.BillingQuestion, Priority = TicketPriority.Low }, _clock.UtcNow);
        await _repository.Add(matching);
        await _repository.Add(wrongCategory);
        await _repository.Add(wrongPriority);
        var handler = new ListTicketsQueryHandler(_repository, new ListTicketsQueryValidator());

        // Act
        var result = await handler.Handle(new ListTicketsQuery(TicketCategory.BillingQuestion, TicketPriority.High), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var ticket = Assert.Single(result.Value);
        Assert.Equal(matching.Id, ticket.Id);
    }

    [Fact]
    public async Task UpdateTicket_WithPartialChanges_OverridesCategoryPriorityAndUpdatesTimestamp()
    {
        // Arrange
        var createdAt = _clock.UtcNow;
        var ticket = CreateTicket(ValidCreateCommand(), createdAt);
        await _repository.Add(ticket);
        _clock.UtcNow = createdAt.AddHours(1);
        var handler = new UpdateTicketCommandHandler(_repository, new UpdateTicketCommandValidator(), _clock);
        var command = new UpdateTicketCommand(
            ticket.Id,
            Subject: "Updated billing question",
            Category: TicketCategory.FeatureRequest,
            Priority: TicketPriority.Low,
            Status: TicketStatus.Resolved);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Updated billing question", result.Value.Subject);
        Assert.Equal(TicketCategory.FeatureRequest, result.Value.Category);
        Assert.Equal(TicketPriority.Low, result.Value.Priority);
        Assert.Equal(TicketStatus.Resolved, result.Value.Status);
        Assert.Equal(_clock.UtcNow, result.Value.UpdatedAt);
        Assert.Equal(_clock.UtcNow, result.Value.ResolvedAt);
    }

    [Fact]
    public async Task UpdateTicket_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var handler = new UpdateTicketCommandHandler(_repository, new UpdateTicketCommandValidator(), _clock);
        var command = new UpdateTicketCommand(Guid.NewGuid(), Subject: "Updated subject");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(ApplicationResultStatus.NotFound, result.Status);
        Assert.Contains(result.Errors, error => error.Field == nameof(UpdateTicketCommand.Id));
    }

    [Fact]
    public async Task DeleteTicket_WhenFound_RemovesTicket()
    {
        // Arrange
        var ticket = CreateTicket(ValidCreateCommand(), _clock.UtcNow);
        await _repository.Add(ticket);
        var handler = new DeleteTicketCommandHandler(_repository, new DeleteTicketCommandValidator());

        // Act
        var result = await handler.Handle(new DeleteTicketCommand(ticket.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ticket.Id, result.Value.Id);
        Assert.Empty(_repository.Tickets);
    }

    [Fact]
    public async Task DeleteTicket_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var handler = new DeleteTicketCommandHandler(_repository, new DeleteTicketCommandValidator());
        var unknownId = Guid.NewGuid();

        // Act
        var result = await handler.Handle(new DeleteTicketCommand(unknownId), CancellationToken.None);

        // Assert
        Assert.Equal(ApplicationResultStatus.NotFound, result.Status);
        Assert.Contains(result.Errors, error => error.Field == nameof(DeleteTicketCommand.Id));
    }

    private static CreateTicketCommand ValidCreateCommand()
    {
        return new CreateTicketCommand(
            CustomerId: "customer-1",
            CustomerEmail: "ada@example.com",
            CustomerName: "Ada Lovelace",
            Subject: "Cannot access account",
            Description: "I cannot access my customer account after resetting my password.",
            Category: TicketCategory.AccountAccess,
            Priority: TicketPriority.High,
            Status: TicketStatus.New,
            Tags: ["account", "login"],
            Metadata: new TicketMetadata(TicketSource.WebForm, "Edge", DeviceType.Desktop));
    }

    private static Ticket CreateTicket(CreateTicketCommand command, DateTimeOffset timestamp)
    {
        var result = Ticket.Create(
            new TicketDraft(
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
                command.AssignedTo),
            timestamp);

        return result.Value!;
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class InMemoryTicketRepository : ITicketRepository
    {
        private readonly List<Ticket> _tickets = [];

        public IReadOnlyList<Ticket> Tickets => _tickets;

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
            var tickets = _tickets
                .Where(ticket => !filter.Category.HasValue || ticket.Category == filter.Category.Value)
                .Where(ticket => !filter.Priority.HasValue || ticket.Priority == filter.Priority.Value)
                .Where(ticket => !filter.Status.HasValue || ticket.Status == filter.Status.Value)
                .ToArray();

            return Task.FromResult<IReadOnlyList<Ticket>>(tickets);
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
            var removedCount = _tickets.RemoveAll(ticket => ticket.Id == id);
            return Task.FromResult(removedCount > 0);
        }
    }
}
