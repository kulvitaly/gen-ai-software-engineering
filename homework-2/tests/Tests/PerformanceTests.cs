using System.Diagnostics;
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

public sealed class PerformanceTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;

    public PerformanceTests()
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
    public async Task BulkCsvImport_50Tickets_CompletesUnderThreeSeconds()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var csv = Csv(Enumerable.Range(1, 50).Select(index => CsvRow(index)));

        // Act
        var (elapsed, response) = await Measure(() => client.PostAsync("/tickets/import?format=csv", Text(csv, "text/csv")));
        var summary = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(50, summary.RootElement.GetProperty("successful").GetInt32());
        Assert.True(elapsed < TimeSpan.FromSeconds(3), $"CSV import took {elapsed.TotalMilliseconds} ms.");
    }

    [Fact]
    public async Task BulkJsonImport_20Tickets_CompletesUnderTwoSeconds()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var json = JsonTickets(20);

        // Act
        var (elapsed, response) = await Measure(() => client.PostAsync("/tickets/import?format=json", Text(json, "application/json")));
        var summary = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(20, summary.RootElement.GetProperty("successful").GetInt32());
        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"JSON import took {elapsed.TotalMilliseconds} ms.");
    }

    [Fact]
    public async Task BulkXmlImport_30Tickets_CompletesUnderThreeSeconds()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var xml = XmlTickets(30);

        // Act
        var (elapsed, response) = await Measure(() => client.PostAsync("/tickets/import?format=xml", Text(xml, "application/xml")));
        var summary = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(30, summary.RootElement.GetProperty("successful").GetInt32());
        Assert.True(elapsed < TimeSpan.FromSeconds(3), $"XML import took {elapsed.TotalMilliseconds} ms.");
    }

    [Fact]
    public async Task AutoClassify_25Tickets_CompletesUnderThreeSeconds()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var created = await Task.WhenAll(Enumerable.Range(1, 25)
            .Select(index => CreateTicket(client, ValidCreateRequest(index) with
            {
                Subject = "Billing refund blocking launch",
                Description = "The payment refund is important and blocking launch.",
                Category = "other",
                Priority = "medium"
            })));
        var ids = created.Select(ticket => ticket.RootElement.GetProperty("id").GetString()).ToArray();

        // Act
        var (elapsed, responses) = await Measure(() => Task.WhenAll(ids.Select(id => client.PostAsync($"/tickets/{id}/auto-classify", content: null))));

        // Assert
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.True(elapsed < TimeSpan.FromSeconds(3), $"Auto-classifying 25 tickets took {elapsed.TotalMilliseconds} ms.");
    }

    [Fact]
    public async Task FilteredList_100Tickets_CompletesUnderTwoSeconds()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var csv = Csv(Enumerable.Range(1, 100).Select(index => CsvRow(
            index,
            category: index % 2 == 0 ? "billing_question" : "technical_issue",
            priority: index % 4 == 0 ? "high" : "low")));
        var importResponse = await client.PostAsync("/tickets/import?format=csv", Text(csv, "text/csv"));
        importResponse.EnsureSuccessStatusCode();

        // Act
        var (elapsed, response) = await Measure(() => client.GetAsync("/tickets?category=billing_question&priority=high"));
        var tickets = await ReadJson(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(25, tickets.RootElement.GetArrayLength());
        Assert.True(elapsed < TimeSpan.FromSeconds(2), $"Filtered list took {elapsed.TotalMilliseconds} ms.");
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

    private static async Task<(TimeSpan Elapsed, T Result)> Measure<T>(Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await action();
        stopwatch.Stop();

        return (stopwatch.Elapsed, result);
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

    private static string Csv(IEnumerable<string> rows)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                "customer_id,customer_email,customer_name,subject,description,category,priority,status,tags,metadata.source,metadata.browser,metadata.device_type,assigned_to"
            }.Concat(rows));
    }

    private static string CsvRow(int index, string category = "billing_question", string priority = "medium")
    {
        return $"customer-{index},perf-{index}@example.com,Customer {index},Billing refund request {index},The payment invoice refund request needs support today.,{category},{priority},new,perf;import,api,Edge,desktop,";
    }

    private static string JsonTickets(int count)
    {
        var tickets = Enumerable.Range(1, count).Select(index => new
        {
            customer_id = $"customer-{index}",
            customer_email = $"json-{index}@example.com",
            customer_name = $"Customer {index}",
            subject = $"JSON billing question {index}",
            description = "The annual invoice payment needs support from the billing team.",
            category = "billing_question",
            priority = "medium",
            status = "new",
            tags = new[] { "json", "perf" },
            metadata = new
            {
                source = "api",
                browser = "Edge",
                device_type = "desktop"
            }
        });

        return JsonSerializer.Serialize(tickets, JsonOptions);
    }

    private static string XmlTickets(int count)
    {
        var tickets = Enumerable.Range(1, count).Select(index =>
            $"""
            <ticket>
              <customer_id>customer-{index}</customer_id>
              <customer_email>xml-{index}@example.com</customer_email>
              <customer_name>Customer {index}</customer_name>
              <subject>XML billing question {index}</subject>
              <description>The annual invoice payment needs support from the billing team.</description>
              <category>billing_question</category>
              <priority>medium</priority>
              <status>new</status>
              <tags><tag>xml</tag><tag>perf</tag></tags>
              <metadata><source>api</source><browser>Edge</browser><device_type>desktop</device_type></metadata>
            </ticket>
            """);

        return $"<tickets>{string.Concat(tickets)}</tickets>";
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

    private sealed record TicketMetadataApiRequest(string? Source, string? Browser, string? DeviceType);
}
