using Application.Common;
using Application.Tickets;
using Domain.Tickets;
using Microsoft.Extensions.Logging;

namespace Tests;

public sealed class CategorizationTests
{
    private readonly TicketClassifier _classifier = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData("Login password 2FA issue", "I cannot access my account after a password reset.", TicketCategory.AccountAccess)]
    [InlineData("Invoice refund needed", "The latest payment and refund details look incorrect.", TicketCategory.BillingQuestion)]
    [InlineData("Application crash", "The portal shows an error and then crashes.", TicketCategory.TechnicalIssue)]
    [InlineData("Feature suggestion", "Please add this enhancement suggestion to exports.", TicketCategory.FeatureRequest)]
    [InlineData("Defect with reproduction", "Here are the steps to reproduce this defect.", TicketCategory.BugReport)]
    public void Classify_WithCategoryKeywords_AssignsExpectedCategory(string subject, string description, TicketCategory expectedCategory)
    {
        // Arrange
        var ticket = CreateTicket(subject: subject, description: description);

        // Act
        var classification = _classifier.Classify(ticket);

        // Assert
        Assert.Equal(expectedCategory, classification.Category);
    }

    [Theory]
    [InlineData("LOGIN issue", "User is experiencing an account access problem.", TicketCategory.AccountAccess)]
    [InlineData("Password reset help", "Customer needs help with resetting their password.", TicketCategory.AccountAccess)]
    [InlineData("2FA not working", "Two-factor authentication is not functioning correctly.", TicketCategory.AccountAccess)]
    [InlineData("CAN'T ACCESS account", "User cannot access their account anymore today.", TicketCategory.AccountAccess)]
    [InlineData("PAYMENT issue", "There is a problem with customer payment processing.", TicketCategory.BillingQuestion)]
    [InlineData("Invoice problem", "Customer has a question about their invoice details.", TicketCategory.BillingQuestion)]
    [InlineData("REFUND requested", "Customer is requesting a refund for recent charges.", TicketCategory.BillingQuestion)]
    [InlineData("BUG report", "Found a bug in the application functionality here.", TicketCategory.TechnicalIssue)]
    [InlineData("ERROR encountered", "An error was encountered during normal usage.", TicketCategory.TechnicalIssue)]
    [InlineData("CRASH happening", "The application is crashing intermittently now.", TicketCategory.TechnicalIssue)]
    [InlineData("FEATURE request", "Customer is requesting a new feature be added.", TicketCategory.FeatureRequest)]
    [InlineData("Enhancement needed", "This enhancement would improve user experience.", TicketCategory.FeatureRequest)]
    [InlineData("SUGGESTION here", "I have a suggestion for product improvements.", TicketCategory.FeatureRequest)]
    [InlineData("REPRODUCTION steps needed", "Here are steps to reproduce this defect now.", TicketCategory.BugReport)]
    public void Classify_WithMixedCaseKeywords_MatchesCaseInsensitively(string subject, string description, TicketCategory expectedCategory)
    {
        // Arrange
        var ticket = CreateTicket(subject: subject, description: description);

        // Act
        var classification = _classifier.Classify(ticket);

        // Assert
        Assert.Equal(expectedCategory, classification.Category);
    }

    [Theory]
    [InlineData("Critical security outage", "Production down and customers cannot access accounts.", TicketPriority.Urgent)]
    [InlineData("Important blocking issue", "This is blocking the release and needs help asap.", TicketPriority.High)]
    [InlineData("Minor cosmetic suggestion", "This suggestion is a small cosmetic polish item.", TicketPriority.Low)]
    [InlineData("General question", "I need help understanding account settings.", TicketPriority.Medium)]
    public void Classify_WithPriorityKeywords_AssignsExpectedPriority(string subject, string description, TicketPriority expectedPriority)
    {
        // Arrange
        var ticket = CreateTicket(subject: subject, description: description);

        // Act
        var classification = _classifier.Classify(ticket);

        // Assert
        Assert.Equal(expectedPriority, classification.Priority);
    }

    [Theory]
    [InlineData("CRITICAL problem", "This is a serious issue affecting our systems.", TicketPriority.Urgent)]
    [InlineData("PRODUCTION DOWN", "Production is down and needs immediate attention.", TicketPriority.Urgent)]
    [InlineData("Security breach", "There is a security issue that needs addressing.", TicketPriority.Urgent)]
    [InlineData("CAN'T ACCESS system", "Customer cannot access the system right now.", TicketPriority.Urgent)]
    [InlineData("CANNOT ACCESS service", "Users cannot access our service at this time.", TicketPriority.Urgent)]
    [InlineData("IMPORTANT request", "This is an important matter for our business.", TicketPriority.High)]
    [InlineData("Blocking issue here", "This is a blocking issue preventing progress.", TicketPriority.High)]
    [InlineData("ASAP needed", "This needs to be resolved as soon as possible.", TicketPriority.High)]
    [InlineData("MINOR issue", "This is a small issue with minor impact here.", TicketPriority.Low)]
    [InlineData("Cosmetic polish", "This is just cosmetic polish for the UI.", TicketPriority.Low)]
    [InlineData("SUGGESTION item", "I have a suggestion for product development.", TicketPriority.Low)]
    public void Classify_WithMixedCasePriorityKeywords_MatchesCaseInsensitively(string subject, string description, TicketPriority expectedPriority)
    {
        // Arrange
        var ticket = CreateTicket(subject: subject, description: description);

        // Act
        var classification = _classifier.Classify(ticket);

        // Assert
        Assert.Equal(expectedPriority, classification.Priority);
    }

    [Fact]
    public async Task AutoClassifyTicket_WhenFound_PersistsDecisionAndLogsOutcome()
    {
        // Arrange
        var repository = new InMemoryTicketRepository();
        var logger = new TestLogger<AutoClassifyTicketCommandHandler>();
        var ticket = CreateTicket(
            subject: "Billing refund blocking launch",
            description: "The payment refund is important and blocking our launch.");
        await repository.Add(ticket);
        var handler = new AutoClassifyTicketCommandHandler(
            repository,
            new AutoClassifyTicketCommandValidator(),
            _classifier,
            _clock,
            logger);

        // Act
        var result = await handler.Handle(new AutoClassifyTicketCommand(ticket.Id), CancellationToken.None);
        var stored = await repository.GetById(ticket.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(TicketCategory.BillingQuestion, result.Value.Category);
        Assert.Equal(TicketPriority.High, result.Value.Priority);
        Assert.NotNull(stored?.Classification);
        Assert.Equal(result.Value.Confidence, stored.Classification.Confidence);
        Assert.Contains(logger.Messages, message => message.Contains(ticket.Id.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, message => message.Contains(nameof(TicketCategory.BillingQuestion), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AutoClassifyTicket_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        var repository = new InMemoryTicketRepository();
        var logger = new TestLogger<AutoClassifyTicketCommandHandler>();
        var handler = new AutoClassifyTicketCommandHandler(
            repository,
            new AutoClassifyTicketCommandValidator(),
            _classifier,
            _clock,
            logger);

        // Act
        var result = await handler.Handle(new AutoClassifyTicketCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.Equal(ApplicationResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateTicket_WithManualCategoryOrPriority_ClearsClassification()
    {
        // Arrange
        var repository = new InMemoryTicketRepository();
        var initial = CreateTicket(
            subject: "Cannot access account",
            description: "I cannot access login because password reset is blocking me.",
            classification: new TicketClassification(
                TicketCategory.AccountAccess,
                TicketPriority.Urgent,
                0.9,
                "Matched access keywords.",
                ["cannot access", "password"]));
        await repository.Add(initial);
        var handler = new UpdateTicketCommandHandler(repository, new UpdateTicketCommandValidator(), _clock);

        // Act
        var result = await handler.Handle(
            new UpdateTicketCommand(initial.Id, Category: TicketCategory.FeatureRequest, Priority: TicketPriority.Low),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Null(result.Value.Classification);
        Assert.Equal(TicketCategory.FeatureRequest, result.Value.Category);
        Assert.Equal(TicketPriority.Low, result.Value.Priority);
    }

    [Fact]
    public async Task UpdateTicket_WithManualClassification_ReplacesClassificationAndLogsFields()
    {
        // Arrange
        var repository = new InMemoryTicketRepository();
        var logger = new TestLogger<UpdateTicketCommandHandler>();
        var initial = CreateTicket(
            subject: "Cannot access account",
            description: "I cannot access login because password reset is blocking me.",
            classification: new TicketClassification(
                TicketCategory.AccountAccess,
                TicketPriority.Urgent,
                0.9,
                "Matched access keywords.",
                ["cannot access", "password"]));
        await repository.Add(initial);
        var handler = new UpdateTicketCommandHandler(repository, new UpdateTicketCommandValidator(), _clock, logger: logger);

        // Act
        var result = await handler.Handle(
            new UpdateTicketCommand(
                initial.Id,
                Category: TicketCategory.FeatureRequest,
                Priority: TicketPriority.Low,
                Classification: new ManualClassification(
                    TicketCategory.FeatureRequest,
                    TicketPriority.Low,
                    0.42,
                    "Manual product request override.",
                    ["roadmap", "feature"])),
            CancellationToken.None);
        var stored = await repository.GetById(initial.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(TicketCategory.FeatureRequest, result.Value.Category);
        Assert.Equal(TicketPriority.Low, result.Value.Priority);
        Assert.NotNull(result.Value.Classification);
        Assert.Equal(0.42, result.Value.Classification.Confidence);
        Assert.Equal(TicketCategory.FeatureRequest, result.Value.Classification.Category);
        Assert.Equal(TicketPriority.Low, result.Value.Classification.Priority);
        Assert.Equal("Manual product request override.", result.Value.Classification.Reasoning);
        Assert.Equal(["roadmap", "feature"], result.Value.Classification.KeywordsFound);
        Assert.Equal(result.Value.Classification, stored?.Classification);
        Assert.Contains(logger.Messages, message => message.Contains(initial.Id.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, message => message.Contains("category", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, message => message.Contains("priority", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, message => message.Contains("classification", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateTicket_WithManualClassificationWithoutOptionalMetadata_DefaultsValues()
    {
        // Arrange
        var repository = new InMemoryTicketRepository();
        var initial = CreateTicket();
        await repository.Add(initial);
        var handler = new UpdateTicketCommandHandler(repository, new UpdateTicketCommandValidator(), _clock);

        // Act
        var result = await handler.Handle(
            new UpdateTicketCommand(
                initial.Id,
                Classification: new ManualClassification(
                    TicketCategory.FeatureRequest,
                    TicketPriority.Low,
                    0.42,
                    null,
                    null)),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value?.Classification);
        Assert.Equal("specified manually", result.Value.Classification.Reasoning);
        Assert.Empty(result.Value.Classification.KeywordsFound);
    }

    [Fact]
    public async Task UpdateTicket_WithInvalidManualClassification_ReturnsValidationError()
    {
        // Arrange
        var repository = new InMemoryTicketRepository();
        var initial = CreateTicket();
        await repository.Add(initial);
        var handler = new UpdateTicketCommandHandler(repository, new UpdateTicketCommandValidator(), _clock);

        // Act
        var result = await handler.Handle(
            new UpdateTicketCommand(
                initial.Id,
                Classification: new ManualClassification(
                    TicketCategory.FeatureRequest,
                    TicketPriority.Low,
                    1.5,
                    "",
                    [])),
            CancellationToken.None);

        // Assert
        Assert.Equal(ApplicationResultStatus.ValidationError, result.Status);
        Assert.Contains(result.Errors, error => error.Field == "Classification.Confidence");
        Assert.DoesNotContain(result.Errors, error => error.Field == "Classification.Reasoning");
        Assert.DoesNotContain(result.Errors, error => error.Field == "Classification.KeywordsFound");
    }

    private static Ticket CreateTicket(
        string subject = "Cannot access account",
        string description = "I cannot access my account after resetting my password.",
        TicketClassification? classification = null)
    {
        var result = Ticket.Create(
            new TicketDraft(
                CustomerId: "customer-1",
                CustomerEmail: "ada@example.com",
                CustomerName: "Ada Lovelace",
                Subject: subject,
                Description: description,
                Category: classification?.Category ?? TicketCategory.Other,
                Priority: classification?.Priority ?? TicketPriority.Medium,
                Status: TicketStatus.New,
                Tags: ["support"],
                Metadata: new TicketMetadata(TicketSource.WebForm, "Edge", DeviceType.Desktop),
                Classification: classification),
            new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero));

        return result.Value!;
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
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
            return Task.FromResult<IReadOnlyList<Ticket>>(_tickets.ToArray());
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

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
