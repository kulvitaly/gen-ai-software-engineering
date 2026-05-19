using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Tickets;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Tests;

public sealed class TicketApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public TicketApiTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<ISqliteConnectionFactory>(_ => new SqliteConnectionFactory($"Data Source={_databasePath}"));
                    services.AddScoped<ITicketRepository, SqliteTicketRepository>();
                });
            });
    }

    [Fact]
    public async Task PostTickets_WithValidBody_ReturnsCreatedTicket()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var request = ValidCreateRequest();

        // Act
        var response = await client.PostAsJsonAsync("/tickets", request, JsonOptions);
        var ticket = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(ticket.RootElement.TryGetProperty("id", out var id));
        Assert.True(Guid.TryParse(id.GetString(), out _));
        Assert.Equal("ada@example.com", ticket.RootElement.GetProperty("customer_email").GetString());
        Assert.Equal("account_access", ticket.RootElement.GetProperty("category").GetString());
        Assert.True(ticket.RootElement.TryGetProperty("created_at", out _));
        Assert.True(ticket.RootElement.TryGetProperty("updated_at", out _));
    }

    [Fact]
    public async Task PostTickets_WithAutoClassifyFlag_ReturnsClassifiedTicket()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var request = ValidCreateRequest() with
        {
            Subject = "Billing refund blocking launch",
            Description = "The payment refund is important and blocking our launch.",
            Category = "other",
            Priority = "medium"
        };

        // Act
        var response = await client.PostAsJsonAsync("/tickets?auto_classify=true", request, JsonOptions);
        var ticket = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("billing_question", ticket.RootElement.GetProperty("category").GetString());
        Assert.Equal("high", ticket.RootElement.GetProperty("priority").GetString());
        var classification = ticket.RootElement.GetProperty("classification");
        Assert.Equal("billing_question", classification.GetProperty("category").GetString());
        Assert.Equal("high", classification.GetProperty("priority").GetString());
        Assert.InRange(classification.GetProperty("confidence").GetDouble(), 0, 1);
        Assert.True(classification.GetProperty("keywords_found").GetArrayLength() > 0);
    }

    [Fact]
    public async Task PostTickets_WithInvalidBody_ReturnsValidationProblem()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var request = ValidCreateRequest() with
        {
            CustomerEmail = "not-an-email",
            Subject = ""
        };

        // Act
        var response = await client.PostAsJsonAsync("/tickets", request, JsonOptions);
        var problem = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("CustomerEmail", out _));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("Subject", out _));
    }

    [Fact]
    public async Task GetTickets_ReturnsCreatedTickets()
    {
        // Arrange
        using var client = _factory.CreateClient();
        await CreateTicket(client, ValidCreateRequest() with { CustomerEmail = "first@example.com" });
        await CreateTicket(client, ValidCreateRequest() with { CustomerEmail = "second@example.com" });

        // Act
        var response = await client.GetAsync("/tickets");
        var tickets = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, tickets.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task GetTickets_WithCombinedFilters_ReturnsMatchingTickets()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var matching = await CreateTicket(client, ValidCreateRequest() with { Category = "billing_question", Priority = "high", Status = "new" });
        await CreateTicket(client, ValidCreateRequest() with { CustomerEmail = "wrong-category@example.com", Category = "technical_issue", Priority = "high", Status = "new" });
        await CreateTicket(client, ValidCreateRequest() with { CustomerEmail = "wrong-priority@example.com", Category = "billing_question", Priority = "low", Status = "new" });

        // Act
        var response = await client.GetAsync("/tickets?category=billing_question&priority=high&status=new");
        var tickets = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ticket = Assert.Single(tickets.RootElement.EnumerateArray());
        Assert.Equal(matching.RootElement.GetProperty("id").GetString(), ticket.GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetTicketById_WhenFound_ReturnsTicket()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var created = await CreateTicket(client, ValidCreateRequest());
        var id = created.RootElement.GetProperty("id").GetString();

        // Act
        var response = await client.GetAsync($"/tickets/{id}");
        var ticket = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, ticket.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetTicketById_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var unknownId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/tickets/{unknownId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostAutoClassify_WhenFound_ReturnsClassificationResponse()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var created = await CreateTicket(client, ValidCreateRequest() with
        {
            Subject = "Cannot access login",
            Description = "This is critical because I cannot access my password reset flow.",
            Category = "other",
            Priority = "medium"
        });
        var id = created.RootElement.GetProperty("id").GetString();

        // Act
        var response = await client.PostAsync($"/tickets/{id}/auto-classify", content: null);
        var classification = await ReadJson(response);
        var getResponse = await client.GetAsync($"/tickets/{id}");
        var ticket = await ReadJson(getResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("account_access", classification.RootElement.GetProperty("category").GetString());
        Assert.Equal("urgent", classification.RootElement.GetProperty("priority").GetString());
        Assert.InRange(classification.RootElement.GetProperty("confidence").GetDouble(), 0, 1);
        Assert.True(classification.RootElement.GetProperty("keywords_found").GetArrayLength() > 0);
        Assert.Equal("account_access", ticket.RootElement.GetProperty("classification").GetProperty("category").GetString());
    }

    [Fact]
    public async Task PostAutoClassify_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var unknownId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/tickets/{unknownId}/auto-classify", content: null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutTicket_WithValidBody_ReturnsUpdatedTicket()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var created = await CreateTicket(client, ValidCreateRequest());
        var id = created.RootElement.GetProperty("id").GetString();
        var request = new UpdateTicketApiRequest(
            CustomerEmail: null,
            CustomerName: null,
            Subject: "Updated subject",
            Description: null,
            Category: "feature_request",
            Priority: "low",
            Status: "resolved",
            Tags: null,
            Metadata: null,
            AssignedTo: null);

        // Act
        var response = await client.PutAsJsonAsync($"/tickets/{id}", request, JsonOptions);
        var ticket = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Updated subject", ticket.RootElement.GetProperty("subject").GetString());
        Assert.Equal("feature_request", ticket.RootElement.GetProperty("category").GetString());
        Assert.Equal("low", ticket.RootElement.GetProperty("priority").GetString());
        Assert.Equal("resolved", ticket.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, ticket.RootElement.GetProperty("resolved_at").ValueKind);
    }

    [Fact]
    public async Task PutTicket_WithManualClassification_ReplacesClassification()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var created = await CreateTicket(client, ValidCreateRequest() with
        {
            Subject = "Billing refund blocking launch",
            Description = "The payment refund is important and blocking our launch.",
            Category = "other",
            Priority = "medium"
        });
        var id = created.RootElement.GetProperty("id").GetString();
        var classifyResponse = await client.PostAsync($"/tickets/{id}/auto-classify", content: null);
        classifyResponse.EnsureSuccessStatusCode();
        var request = new UpdateTicketApiRequest(
            CustomerEmail: null,
            CustomerName: null,
            Subject: null,
            Description: null,
            Category: "feature_request",
            Priority: "low",
            Status: null,
            Tags: null,
            Metadata: null,
            AssignedTo: null,
            Classification: new ClassificationApiRequest(
                Category: "feature_request",
                Priority: "low",
                Confidence: 0.42,
                Reasoning: "Manual product request override.",
                KeywordsFound: ["roadmap", "feature"]));

        // Act
        var response = await client.PutAsJsonAsync($"/tickets/{id}", request, JsonOptions);
        var ticket = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("feature_request", ticket.RootElement.GetProperty("category").GetString());
        Assert.Equal("low", ticket.RootElement.GetProperty("priority").GetString());
        var classification = ticket.RootElement.GetProperty("classification");
        Assert.Equal("feature_request", classification.GetProperty("category").GetString());
        Assert.Equal("low", classification.GetProperty("priority").GetString());
        Assert.Equal(0.42, classification.GetProperty("confidence").GetDouble());
        Assert.Equal("Manual product request override.", classification.GetProperty("reasoning").GetString());
        Assert.Equal(2, classification.GetProperty("keywords_found").GetArrayLength());
    }

    [Fact]
    public async Task PutTicket_WithManualClassificationWithoutOptionalMetadata_DefaultsValues()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var created = await CreateTicket(client, ValidCreateRequest());
        var id = created.RootElement.GetProperty("id").GetString();
        using var content = new StringContent(
            """
            {
              "classification": {
                "category": "feature_request",
                "priority": "low",
                "confidence": 0.42
              }
            }
            """,
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PutAsync($"/tickets/{id}", content);
        var ticket = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var classification = ticket.RootElement.GetProperty("classification");
        Assert.Equal("feature_request", classification.GetProperty("category").GetString());
        Assert.Equal("low", classification.GetProperty("priority").GetString());
        Assert.Equal(0.42, classification.GetProperty("confidence").GetDouble());
        Assert.Equal("specified manually", classification.GetProperty("reasoning").GetString());
        Assert.Equal(0, classification.GetProperty("keywords_found").GetArrayLength());
    }

    [Fact]
    public async Task PutTicket_WithAutoClassifyFlag_ReclassifiesUpdatedTicket()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var created = await CreateTicket(client, ValidCreateRequest() with
        {
            Category = "other",
            Priority = "medium"
        });
        var id = created.RootElement.GetProperty("id").GetString();
        var request = new UpdateTicketApiRequest(
            CustomerEmail: null,
            CustomerName: null,
            Subject: "Billing refund blocking launch",
            Description: "The payment refund is important and blocking our launch.",
            Category: "feature_request",
            Priority: "low",
            Status: null,
            Tags: null,
            Metadata: null,
            AssignedTo: null,
            Classification: new ClassificationApiRequest(
                Category: "feature_request",
                Priority: "low",
                Confidence: 0.42,
                Reasoning: "Manual product request override.",
                KeywordsFound: ["roadmap"]));

        // Act
        var response = await client.PutAsJsonAsync($"/tickets/{id}?auto_classify=true", request, JsonOptions);
        var ticket = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("billing_question", ticket.RootElement.GetProperty("category").GetString());
        Assert.Equal("high", ticket.RootElement.GetProperty("priority").GetString());
        var classification = ticket.RootElement.GetProperty("classification");
        Assert.Equal("billing_question", classification.GetProperty("category").GetString());
        Assert.Equal("high", classification.GetProperty("priority").GetString());
        Assert.InRange(classification.GetProperty("confidence").GetDouble(), 0, 1);
        Assert.True(classification.GetProperty("keywords_found").GetArrayLength() > 0);
    }

    [Fact]
    public async Task PutTicket_WithStandaloneConfidenceScore_DoesNotReplaceClassification()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var created = await CreateTicket(client, ValidCreateRequest() with
        {
            Subject = "Billing refund blocking launch",
            Description = "The payment refund is important and blocking our launch.",
            Category = "other",
            Priority = "medium"
        });
        var id = created.RootElement.GetProperty("id").GetString();
        var classifyResponse = await client.PostAsync($"/tickets/{id}/auto-classify", content: null);
        classifyResponse.EnsureSuccessStatusCode();
        using var content = new StringContent(
            """
            {
              "confidence_score": 0.42
            }
            """,
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PutAsync($"/tickets/{id}", content);
        var ticket = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var classification = ticket.RootElement.GetProperty("classification");
        Assert.Equal("billing_question", classification.GetProperty("category").GetString());
        Assert.Equal("high", classification.GetProperty("priority").GetString());
        Assert.NotEqual(0.42, classification.GetProperty("confidence").GetDouble());
    }

    [Fact]
    public async Task PutTicket_WithInvalidBody_ReturnsValidationProblem()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var created = await CreateTicket(client, ValidCreateRequest());
        var id = created.RootElement.GetProperty("id").GetString();
        var request = new UpdateTicketApiRequest(
            CustomerEmail: "bad-email",
            CustomerName: null,
            Subject: "",
            Description: null,
            Category: null,
            Priority: null,
            Status: null,
            Tags: null,
            Metadata: null,
            AssignedTo: null,
            Classification: new ClassificationApiRequest(
                Category: "feature_request",
                Priority: "low",
                Confidence: 1.5,
                Reasoning: "",
                KeywordsFound: null));

        // Act
        var response = await client.PutAsJsonAsync($"/tickets/{id}", request, JsonOptions);
        var problem = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("CustomerEmail", out _));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("Subject", out _));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("Classification.Confidence", out _));
        Assert.False(problem.RootElement.GetProperty("errors").TryGetProperty("Classification.Reasoning", out _));
        Assert.False(problem.RootElement.GetProperty("errors").TryGetProperty("Classification.KeywordsFound", out _));
    }

    [Fact]
    public async Task PutTicket_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var unknownId = Guid.NewGuid();
        var request = new UpdateTicketApiRequest(null, null, "Updated subject", null, null, null, null, null, null, null);

        // Act
        var response = await client.PutAsJsonAsync($"/tickets/{unknownId}", request, JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTicket_WhenFound_RemovesTicket()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var created = await CreateTicket(client, ValidCreateRequest());
        var id = created.RootElement.GetProperty("id").GetString();

        // Act
        var deleteResponse = await client.DeleteAsync($"/tickets/{id}");
        var getResponse = await client.GetAsync($"/tickets/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteTicket_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var unknownId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/tickets/{unknownId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ImportTickets_WithPartialCsvFailures_ReturnsSummary()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var csv = string.Join(
            Environment.NewLine,
            "customer_id,customer_email,customer_name,subject,description,category,priority,status,tags,metadata.source,metadata.browser,metadata.device_type,assigned_to",
            "customer-1,ada@example.com,Ada Lovelace,Cannot access account,I cannot access my customer account after resetting my password.,account_access,high,new,account;login,web_form,Edge,desktop,",
            "customer-2,bad-email,Grace Hopper,Billing invoice question,I need help understanding the latest annual invoice.,billing_question,medium,new,billing;invoice,email,Firefox,desktop,");

        // Act
        var response = await client.PostAsync("/tickets/import?format=csv", Text(csv, "text/csv"));
        var summary = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, summary.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, summary.RootElement.GetProperty("successful").GetInt32());
        var failure = Assert.Single(summary.RootElement.GetProperty("failed").EnumerateArray());
        Assert.Equal(2, failure.GetProperty("record_number").GetInt32());
    }

    [Fact]
    public async Task ImportTickets_WithUnusableFile_ReturnsValidationProblem()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/tickets/import?format=csv", Text("customer_id,customer_email", "text/csv"));
        var problem = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("Content", out _));
    }

    [Fact]
    public async Task ImportTickets_WithUnsupportedFormat_ReturnsValidationProblem()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/tickets/import?format=yaml", Text("tickets: []", "text/plain"));
        var problem = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("format", out _));
    }

    public void Dispose()
    {
        _factory.Dispose();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static async Task<JsonDocument> CreateTicket(HttpClient client, CreateTicketApiRequest request)
    {
        var response = await client.PostAsJsonAsync("/tickets", request, JsonOptions);
        response.EnsureSuccessStatusCode();

        return await ReadJson(response);
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static StringContent Text(string content, string mediaType)
    {
        return new StringContent(content, Encoding.UTF8, mediaType);
    }

    private static CreateTicketApiRequest ValidCreateRequest()
    {
        return new CreateTicketApiRequest(
            CustomerId: "customer-1",
            CustomerEmail: "ada@example.com",
            CustomerName: "Ada Lovelace",
            Subject: "Cannot access account",
            Description: "I cannot access my customer account after resetting my password.",
            Category: "account_access",
            Priority: "high",
            Status: "new",
            Tags: ["account", "login"],
            Metadata: new TicketMetadataApiRequest("web_form", "Edge", "desktop"),
            AssignedTo: null);
    }

    private sealed record CreateTicketApiRequest(
        string? CustomerId,
        string? CustomerEmail,
        string? CustomerName,
        string? Subject,
        string? Description,
        string? Category,
        string? Priority,
        string? Status,
        IReadOnlyCollection<string>? Tags,
        TicketMetadataApiRequest? Metadata,
        string? AssignedTo);

    private sealed record UpdateTicketApiRequest(
        string? CustomerEmail,
        string? CustomerName,
        string? Subject,
        string? Description,
        string? Category,
        string? Priority,
        string? Status,
        IReadOnlyCollection<string>? Tags,
        TicketMetadataApiRequest? Metadata,
        string? AssignedTo,
        ClassificationApiRequest? Classification = null);

    private sealed record TicketMetadataApiRequest(string? Source, string? Browser, string? DeviceType);

    private sealed record ClassificationApiRequest(
        string? Category,
        string? Priority,
        double Confidence,
        string? Reasoning,
        IReadOnlyCollection<string>? KeywordsFound);
}
