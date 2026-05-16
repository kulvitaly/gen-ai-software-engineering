using Domain.Tickets;

namespace Tests;

public sealed class TicketModelTests
{
    [Fact]
    public void Create_WithValidDraft_ReturnsTicketWithGeneratedIdAndTimestamps()
    {
        var now = new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);

        var result = Ticket.Create(ValidDraft(), now);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Value);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal(now, result.Value.CreatedAt);
        Assert.Equal(now, result.Value.UpdatedAt);
        Assert.Null(result.Value.ResolvedAt);
        Assert.Equal(TicketStatus.New, result.Value.Status);
        Assert.Equal(["import", "phase-1"], result.Value.Tags);
        Assert.Equal(TicketSource.WebForm, result.Value.Metadata.Source);
        Assert.Equal(DeviceType.Desktop, result.Value.Metadata.DeviceType);
    }

    [Theory]
    [InlineData(nameof(TicketDraft.CustomerId))]
    [InlineData(nameof(TicketDraft.CustomerEmail))]
    [InlineData(nameof(TicketDraft.CustomerName))]
    [InlineData(nameof(TicketDraft.Subject))]
    [InlineData(nameof(TicketDraft.Description))]
    public void Create_WhenRequiredStringIsMissing_ReturnsValidationError(string field)
    {
        var draft = ValidDraft() with
        {
            CustomerId = field == nameof(TicketDraft.CustomerId) ? " " : "customer-1",
            CustomerEmail = field == nameof(TicketDraft.CustomerEmail) ? "" : "ada@example.com",
            CustomerName = field == nameof(TicketDraft.CustomerName) ? null : "Ada Lovelace",
            Subject = field == nameof(TicketDraft.Subject) ? " " : "Cannot access account",
            Description = field == nameof(TicketDraft.Description) ? null : "I cannot access my customer account."
        };

        var result = Ticket.Create(draft);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Field == field);
    }

    [Fact]
    public void Create_WhenEmailIsInvalid_ReturnsValidationError()
    {
        var result = Ticket.Create(ValidDraft() with { CustomerEmail = "not-an-email" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Field == nameof(TicketDraft.CustomerEmail));
    }

    [Theory]
    [InlineData("")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void Create_WhenSubjectLengthIsInvalid_ReturnsValidationError(string subject)
    {
        var result = Ticket.Create(ValidDraft() with { Subject = subject });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Field == nameof(TicketDraft.Subject));
    }

    [Theory]
    [InlineData("too short")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void Create_WhenDescriptionLengthIsInvalid_ReturnsValidationError(string description)
    {
        var invalidDescription = description == "too short" ? description : new string('x', 2001);
        var result = Ticket.Create(ValidDraft() with { Description = invalidDescription });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Field == nameof(TicketDraft.Description));
    }

    [Fact]
    public void Enums_ContainRequiredTaskValues()
    {
        Assert.Equal(
            [TicketCategory.AccountAccess, TicketCategory.TechnicalIssue, TicketCategory.BillingQuestion, TicketCategory.FeatureRequest, TicketCategory.BugReport, TicketCategory.Other],
            Enum.GetValues<TicketCategory>());
        Assert.Equal([TicketPriority.Urgent, TicketPriority.High, TicketPriority.Medium, TicketPriority.Low], Enum.GetValues<TicketPriority>());
        Assert.Equal([TicketStatus.New, TicketStatus.InProgress, TicketStatus.WaitingCustomer, TicketStatus.Resolved, TicketStatus.Closed], Enum.GetValues<TicketStatus>());
    }

    [Fact]
    public void Create_WhenEnumValueIsUndefined_ReturnsValidationError()
    {
        var result = Ticket.Create(ValidDraft() with { Category = (TicketCategory)999 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Field == nameof(TicketDraft.Category));
    }

    [Fact]
    public void Create_WhenMetadataIsInvalid_ReturnsValidationErrors()
    {
        var result = Ticket.Create(ValidDraft() with { Metadata = new TicketMetadata((TicketSource)999, "Edge", null) });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Field == "Metadata.Source");
        Assert.Contains(result.Errors, error => error.Field == "Metadata.DeviceType");
    }

    [Fact]
    public void MarkResolved_UpdatesStatusResolvedAtAndUpdatedAt()
    {
        var created = new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);
        var resolved = created.AddHours(2);
        var ticket = Ticket.Create(ValidDraft(), created).Value!;

        ticket.ChangeStatus(TicketStatus.Resolved, resolved);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(resolved, ticket.ResolvedAt);
        Assert.Equal(resolved, ticket.UpdatedAt);
    }

    private static TicketDraft ValidDraft()
    {
        return new TicketDraft(
            CustomerId: "customer-1",
            CustomerEmail: "ada@example.com",
            CustomerName: "Ada Lovelace",
            Subject: "Cannot access account",
            Description: "I cannot access my customer account after resetting my password.",
            Category: TicketCategory.AccountAccess,
            Priority: TicketPriority.High,
            Status: TicketStatus.New,
            Tags: ["import", "phase-1"],
            Metadata: new TicketMetadata(TicketSource.WebForm, "Edge", DeviceType.Desktop));
    }
}
