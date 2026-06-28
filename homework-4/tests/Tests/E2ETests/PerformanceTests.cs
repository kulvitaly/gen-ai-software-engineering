using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Tickets;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

namespace Tests;

[Trait("Category", "Performance")]
public sealed class PerformanceTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private const double AllowedFailurePercent = 0;

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
    public void BulkCsvImport_50Tickets_MeetsNBomberQualityGates()
    {
        // Arrange
        using var client = _factory.CreateClient();
        WarmUp(client);
        var csv = Csv(Enumerable.Range(1, 50).Select(index => CsvRow(index)));

        // Act
        var stepStats = RunSingleStepScenario(
            "bulk_csv_import_50",
            "post_csv_import",
            async () =>
            {
                var response = await client.PostAsync("/tickets/import?format=csv", Text(csv, "text/csv"));
                var summary = await ReadJson(response);
                return response.StatusCode == HttpStatusCode.OK
                    && summary.RootElement.GetProperty("successful").GetInt32() == 50;
            });

        // Assert
        AssertQualityGates(stepStats, averageMs: 1_500, maxMs: 3_000, p95Ms: 3_000);
    }

    [Fact]
    public void BulkJsonImport_20Tickets_MeetsNBomberQualityGates()
    {
        // Arrange
        using var client = _factory.CreateClient();
        WarmUp(client);
        var json = JsonTickets(20);

        // Act
        var stepStats = RunSingleStepScenario(
            "bulk_json_import_20",
            "post_json_import",
            async () =>
            {
                var response = await client.PostAsync("/tickets/import?format=json", Text(json, "application/json"));
                var summary = await ReadJson(response);
                return response.StatusCode == HttpStatusCode.OK
                    && summary.RootElement.GetProperty("successful").GetInt32() == 20;
            });

        // Assert
        AssertQualityGates(stepStats, averageMs: 1_000, maxMs: 2_000, p95Ms: 2_000);
    }

    [Fact]
    public void BulkXmlImport_30Tickets_MeetsNBomberQualityGates()
    {
        // Arrange
        using var client = _factory.CreateClient();
        WarmUp(client);
        var xml = XmlTickets(30);

        // Act
        var stepStats = RunSingleStepScenario(
            "bulk_xml_import_30",
            "post_xml_import",
            async () =>
            {
                var response = await client.PostAsync("/tickets/import?format=xml", Text(xml, "application/xml"));
                var summary = await ReadJson(response);
                return response.StatusCode == HttpStatusCode.OK
                    && summary.RootElement.GetProperty("successful").GetInt32() == 30;
            });

        // Assert
        AssertQualityGates(stepStats, averageMs: 1_500, maxMs: 3_000, p95Ms: 3_000);
    }

    [Fact]
    public async Task FilteredList_100Tickets_MeetsNBomberQualityGates()
    {
        // Arrange
        using var client = _factory.CreateClient();
        WarmUp(client);
        var csv = Csv(Enumerable.Range(1, 100).Select(index => CsvRow(
            index,
            category: index % 2 == 0 ? "billing_question" : "technical_issue",
            priority: index % 4 == 0 ? "high" : "low")));
        var importResponse = await client.PostAsync("/tickets/import?format=csv", Text(csv, "text/csv"));
        importResponse.EnsureSuccessStatusCode();

        // Act
        var stepStats = RunSingleStepScenario(
            "filtered_list_100",
            "get_filtered_tickets",
            async () =>
            {
                var response = await client.GetAsync("/tickets?category=billing_question&priority=high");
                var tickets = await ReadJson(response);
                return response.StatusCode == HttpStatusCode.OK
                    && tickets.RootElement.GetArrayLength() == 25;
            });

        // Assert
        AssertQualityGates(stepStats, averageMs: 500, maxMs: 2_000, p95Ms: 2_000);
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

    private static void WarmUp(HttpClient client)
    {
        var response = client.GetAsync("/health").GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    private static StepStats RunSingleStepScenario(string scenarioName, string stepName, Func<Task<bool>> request)
    {
        var scenario = Scenario.Create(scenarioName, async context =>
            await Step.Run(stepName, context, async () =>
            {
                var isSuccess = await request();
                return isSuccess ? Response.Ok() : Response.Fail();
            }))
            .WithoutWarmUp()
            .WithLoadSimulations(Simulation.Inject(
                rate: 1,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(1)));

        var result = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        return result.ScenarioStats.Get(scenarioName).StepStats.Get(stepName);
    }

    private static void AssertQualityGates(StepStats stats, double averageMs, double maxMs, double p95Ms)
    {
        Assert.True(stats.Ok.Request.Count > 0, "NBomber did not record successful requests.");
        Assert.True(stats.Fail.Request.Percent <= AllowedFailurePercent, $"Failure rate was {stats.Fail.Request.Percent}%.");
        Assert.True(stats.Ok.Latency.MeanMs <= averageMs, $"Average request execution time was {stats.Ok.Latency.MeanMs} ms; gate is {averageMs} ms.");
        Assert.True(stats.Ok.Latency.MaxMs <= maxMs, $"Max request execution time was {stats.Ok.Latency.MaxMs} ms; gate is {maxMs} ms.");
        Assert.True(stats.Ok.Latency.Percent95 <= p95Ms, $"P95 request execution time was {stats.Ok.Latency.Percent95} ms; gate is {p95Ms} ms.");
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
