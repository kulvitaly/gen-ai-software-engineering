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

public sealed class IntegrationTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationTests()
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
    public async Task BulkImportThenAutoClassify_StoresClassificationOnSelectedRows()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var csv = Csv(
            CsvRow(1, "Billing refund blocking launch", "The payment refund is important and blocking launch.", "other", "medium"),
            CsvRow(2, "Cosmetic feature suggestion", "This minor cosmetic suggestion can wait for later.", "other", "medium"));

        // Act
        var importResponse = await client.PostAsync("/tickets/import?format=csv", Text(csv, "text/csv"));
        var ticketsResponse = await client.GetAsync("/tickets");
        var tickets = await ReadJson(ticketsResponse);
        var firstTicketId = tickets.RootElement.EnumerateArray().First().GetProperty("id").GetString();
        var classifyResponse = await client.PostAsync($"/tickets/{firstTicketId}/auto-classify", content: null);
        var classifiedResponse = await client.GetAsync($"/tickets/{firstTicketId}");
        var classifiedTicket = await ReadJson(classifiedResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, classifyResponse.StatusCode);
        Assert.Equal("billing_question", classifiedTicket.RootElement.GetProperty("category").GetString());
        Assert.Equal("high", classifiedTicket.RootElement.GetProperty("priority").GetString());
        Assert.Equal("billing_question", classifiedTicket.RootElement.GetProperty("classification").GetProperty("category").GetString());
    }

    [Fact]
    public async Task ImportClassifyAndFilter_ReturnsClassifiedTicketInFilteredList()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var csv = Csv(CsvRow(1, "Cannot access login", "This is critical because I cannot access password reset.", "other", "medium"));

        // Act
        var importResponse = await client.PostAsync("/tickets/import?format=csv", Text(csv, "text/csv"));
        var importedTickets = await ReadJson(await client.GetAsync("/tickets"));
        var id = importedTickets.RootElement.EnumerateArray().Single().GetProperty("id").GetString();
        var classifyResponse = await client.PostAsync($"/tickets/{id}/auto-classify", content: null);
        var filteredResponse = await client.GetAsync("/tickets?category=account_access&priority=urgent");
        var filteredTickets = await ReadJson(filteredResponse);

        // Assert
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, classifyResponse.StatusCode);
        var ticket = Assert.Single(filteredTickets.RootElement.EnumerateArray());
        Assert.Equal(id, ticket.GetProperty("id").GetString());
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

    private static string Csv(params string[] rows)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                "customer_id,customer_email,customer_name,subject,description,category,priority,status,tags,metadata.source,metadata.browser,metadata.device_type,assigned_to"
            }.Concat(rows));
    }

    private static string CsvRow(int index, string subject, string description, string category, string priority)
    {
        return $"customer-{index},customer-{index}@example.com,Customer {index},{subject},{description},{category},{priority},new,support;phase7,web_form,Edge,desktop,";
    }

    private static CreateTicketApiRequest ValidCreateRequest(int index = 1)
    {
        return new CreateTicketApiRequest(
            CustomerId: $"customer-{index}",
            CustomerEmail: $"customer-{index}@example.com",
            CustomerName: $"Customer {index}",
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
        string? CustomerEmail = null,
        string? CustomerName = null,
        string? Subject = null,
        string? Description = null,
        string? Category = null,
        string? Priority = null,
        string? Status = null,
        IReadOnlyCollection<string>? Tags = null,
        TicketMetadataApiRequest? Metadata = null,
        string? AssignedTo = null);

    private sealed record TicketMetadataApiRequest(string? Source, string? Browser, string? DeviceType);
}
