using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TransactionApi.Application.Commands.CreateTransaction;
using TransactionApi.Tests.Fixtures;

namespace TransactionApi.Tests.Integration;

public class TransactionEndpointsTests : IClassFixture<PerTestClassSqliteFixture>, IAsyncLifetime
{
    private readonly PerTestClassSqliteFixture _fixture;
    private HttpClient _client = null!;

    public TransactionEndpointsTests(PerTestClassSqliteFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ClearAllDataAsync();
        _client = _fixture.Factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateTransaction_WithValidData_ReturnsCreatedStatus()
    {
        // Arrange
        var command = new
        {
            fromAccount = "ACC-12345",
            toAccount = "ACC-67890",
            amount = 100.50m,
            currency = "USD",
            type = "Transfer"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/transactions", command);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("id", out _));
    }

    [Fact]
    public async Task CreateTransaction_WithInvalidAmount_ReturnsBadRequest()
    {
        // Arrange
        var command = new
        {
            fromAccount = "ACC-12345",
            toAccount = "ACC-67890",
            amount = -100m,
            currency = "USD",
            type = "Transfer"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/transactions", command);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_WithInvalidCurrency_ReturnsBadRequest()
    {
        // Arrange
        var command = new
        {
            fromAccount = "ACC-12345",
            toAccount = "ACC-67890",
            amount = 100m,
            currency = "INVALID",
            type = "Transfer"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/transactions", command);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTransaction_ValidationFailure_ReturnsDetails_with_field_and_message()
    {
        var response = await _client.PostAsJsonAsync("/transactions", new
        {
            type = "Transfer",
            fromAccount = (string?)null,
            toAccount = "ACC-10002",
            amount = 10m,
            currency = "USD"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Validation failed", doc.GetProperty("error").GetString());
        var details = doc.GetProperty("details");
        Assert.Equal(JsonValueKind.Array, details.ValueKind);
        Assert.Contains(
            details.EnumerateArray(),
            row =>
                row.GetProperty("field").GetString() == nameof(CreateTransactionCommand.FromAccount)
                && row.GetProperty("message").GetString() == "From account is required for a transfer.");
    }

    [Fact]
    public async Task GetAllTransactions_ReturnsOkWithEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/transactions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllTransactions_AfterCreatingTransaction_ReturnsOkWithTransactions()
    {
        // Arrange
        var command = new
        {
            fromAccount = "ACC-12345",
            toAccount = "ACC-67890",
            amount = 100.50m,
            currency = "USD",
            type = "Transfer"
        };

        await _client.PostAsJsonAsync("/transactions", command);

        // Act
        var response = await _client.GetAsync("/transactions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetTransactionById_WithValidId_ReturnsOk()
    {
        // Arrange
        var createCommand = new
        {
            fromAccount = "ACC-12345",
            toAccount = "ACC-67890",
            amount = 100.50m,
            currency = "USD",
            type = "Transfer"
        };

        var createResponse = await _client.PostAsJsonAsync("/transactions", createCommand);
        var createdResult = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdResult.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/transactions/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<object>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetTransactionById_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/transactions/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAccountBalance_WithNoTransactions_ReturnsZero()
    {
        // Act
        var response = await _client.GetAsync("/accounts/ACC-12345/balance");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0m, result.GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task GetAccountBalance_WithTransactions_ReturnsCorrectBalance()
    {
        // Arrange - Create a deposit to ACC-12345
        var depositCommand = new
        {
            toAccount = "ACC-12345",
            amount = 100m,
            currency = "USD",
            type = "Deposit"
        };

        await _client.PostAsJsonAsync("/transactions", depositCommand);

        // Arrange - Create a withdrawal from ACC-12345
        var withdrawalCommand = new
        {
            fromAccount = "ACC-12345",
            amount = 30m,
            currency = "USD",
            type = "Withdrawal"
        };

        await _client.PostAsJsonAsync("/transactions", withdrawalCommand);

        // Act
        var response = await _client.GetAsync("/accounts/ACC-12345/balance");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(70m, result.GetProperty("balance").GetDecimal()); // 100 - 30
    }

    [Fact]
    public async Task GetTransactions_FilterByAccountId_ReturnsOnlyTransactionsForThatAccount()
    {
        // Arrange - Create transactions with different accounts
        await _client.PostAsJsonAsync("/transactions", new
        {
            fromAccount = "ACC-111",
            toAccount = "ACC-222",
            amount = 100m,
            currency = "USD",
            type = "Transfer"
        });

        await _client.PostAsJsonAsync("/transactions", new
        {
            fromAccount = "ACC-222",
            toAccount = "ACC-333",
            amount = 50m,
            currency = "USD",
            type = "Transfer"
        });

        // Act - Filter by ACC-111 (should include as FromAccount)
        var response = await _client.GetAsync("/transactions?accountId=ACC-111");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetTransactions_FilterByType_ReturnsOnlyTransactionsOfThatType()
    {
        // Arrange - Create transactions with different types
        await _client.PostAsJsonAsync("/transactions", new
        {
            toAccount = "ACC-111",
            amount = 100m,
            currency = "USD",
            type = "Deposit"
        });

        await _client.PostAsJsonAsync("/transactions", new
        {
            fromAccount = "ACC-222",
            toAccount = "ACC-333",
            amount = 50m,
            currency = "USD",
            type = "Transfer"
        });

        // Act - Filter by Deposit type
        var response = await _client.GetAsync("/transactions?type=Deposit");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetTransactions_FilterByType_IsCaseInsensitive()
    {
        // Arrange - Create a Deposit transaction
        await _client.PostAsJsonAsync("/transactions", new
        {
            toAccount = "ACC-111",
            amount = 100m,
            currency = "USD",
            type = "Deposit"
        });

        // Act - Filter using lowercase type
        var response = await _client.GetAsync("/transactions?type=deposit");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetTransactions_FilterByDateRange_ReturnsTransactionsWithinRange()
    {
        // Arrange - This test will work once we can create transactions with specific timestamps
        // For now, we'll create transactions and verify the endpoint accepts the parameters
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);
        var oneHourLater = now.AddHours(1);

        await _client.PostAsJsonAsync("/transactions", new
        {
            toAccount = "ACC-111",
            amount = 100m,
            currency = "USD",
            type = "Deposit"
        });

        // Act - Filter with date range
        var response = await _client.GetAsync($"/transactions?from={oneHourAgo:O}&to={oneHourLater:O}");

        // Assert - Should return the transaction created within the range
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetTransactions_CombineMultipleFilters_ReturnsOnlyTransactionsMatchingAllCriteria()
    {
        // Arrange - Create various transactions
        await _client.PostAsJsonAsync("/transactions", new
        {
            fromAccount = "ACC-111",
            toAccount = "ACC-222",
            amount = 100m,
            currency = "USD",
            type = "Transfer"
        });

        await _client.PostAsJsonAsync("/transactions", new
        {
            toAccount = "ACC-111",
            amount = 50m,
            currency = "USD",
            type = "Deposit"
        });

        await _client.PostAsJsonAsync("/transactions", new
        {
            fromAccount = "ACC-222",
            toAccount = "ACC-333",
            amount = 75m,
            currency = "USD",
            type = "Transfer"
        });

        // Act - Filter by accountId AND type
        var response = await _client.GetAsync("/transactions?accountId=ACC-111&type=Transfer");

        // Assert - Should only return the first transaction (ACC-111 involved AND type is Transfer)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task GetAccountSummary_WithNoTransactions_ReturnsZerosAndNullRecentDate()
    {
        var response = await _client.GetAsync("/accounts/ACC-SUMMARY-NONE/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ACC-SUMMARY-NONE", doc.GetProperty("accountId").GetString());
        Assert.Equal(0m, doc.GetProperty("totalDeposits").GetDecimal());
        Assert.Equal(0m, doc.GetProperty("totalWithdrawals").GetDecimal());
        Assert.Equal(0, doc.GetProperty("transactionCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, doc.GetProperty("mostRecentTransactionDate").ValueKind);
    }

    [Fact]
    public async Task GetAccountSummary_AggregatesDepositsWithdrawalsCountsAndLatestTimestamp()
    {
        await _client.PostAsJsonAsync("/transactions", new
        {
            toAccount = "ACC-SUMMARY",
            amount = 100m,
            currency = "USD",
            type = "Deposit"
        });

        await _client.PostAsJsonAsync("/transactions", new
        {
            toAccount = "ACC-SUMMARY",
            amount = 40m,
            currency = "USD",
            type = "Deposit"
        });

        await _client.PostAsJsonAsync("/transactions", new
        {
            fromAccount = "ACC-SUMMARY",
            amount = 25m,
            currency = "USD",
            type = "Withdrawal"
        });

        await _client.PostAsJsonAsync("/transactions", new
        {
            fromAccount = "ACC-SUMMARY",
            toAccount = "ACC-OTHER",
            amount = 10m,
            currency = "USD",
            type = "Transfer"
        });

        var response = await _client.GetAsync("/accounts/ACC-SUMMARY/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(140m, doc.GetProperty("totalDeposits").GetDecimal());
        Assert.Equal(25m, doc.GetProperty("totalWithdrawals").GetDecimal());
        Assert.Equal(4, doc.GetProperty("transactionCount").GetInt32());
        Assert.True(doc.GetProperty("mostRecentTransactionDate").TryGetDateTime(out _), "Expected an ISO8601 timestamp string.");
    }

    [Fact]
    public async Task GetAccountInterest_WithZeroBalance_ReturnsZeroInterest()
    {
        var response = await _client.GetAsync("/accounts/ACC-INTEREST-EMPTY/interest?rate=0.05&days=30");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ACC-INTEREST-EMPTY", doc.GetProperty("accountId").GetString());
        Assert.Equal(0m, doc.GetProperty("principal").GetDecimal());
        Assert.Equal(0.05m, doc.GetProperty("rate").GetDecimal());
        Assert.Equal(30, doc.GetProperty("days").GetInt32());
        Assert.Equal(0m, doc.GetProperty("interest").GetDecimal());
    }

    [Fact]
    public async Task GetAccountInterest_FullYearAtFivePercent_ReturnsFiftyOnThousandPrincipal()
    {
        var depositResponse = await _client.PostAsJsonAsync("/transactions", new
        {
            toAccount = "ACC-INTERESTPRIN",
            amount = 1000m,
            currency = "USD",
            type = "Deposit"
        });
        Assert.Equal(HttpStatusCode.Created, depositResponse.StatusCode);

        var response = await _client.GetAsync("/accounts/ACC-INTERESTPRIN/interest?rate=0.05&days=365");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1000m, doc.GetProperty("principal").GetDecimal());
        Assert.Equal(50m, doc.GetProperty("interest").GetDecimal());
    }

    [Fact]
    public async Task GetAccountInterest_NegativeRate_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/accounts/ACC-INTEREST/interest?rate=-0.01&days=10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("rate and days must be non-negative.", doc.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GetAccountInterest_NegativeDays_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/accounts/ACC-INTEREST/interest?rate=0.05&days=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("rate and days must be non-negative.", doc.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ExportTransactions_MissingFormat_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/transactions/export");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.String, doc.GetProperty("error").ValueKind);
        Assert.Contains("format", doc.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportTransactions_UnsupportedFormat_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/transactions/export?format=json");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("csv", doc.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportTransactions_FormatCsv_Uppercase_ReturnsOkWithCsvContentType()
    {
        var response = await _client.GetAsync("/transactions/export?format=CSV");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ExportTransactions_FormatCsv_WithNoTransactions_ReturnsHeaderOnly()
    {
        var response = await _client.GetAsync("/transactions/export?format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var normalized = body.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        Assert.Equal(
            "Id,FromAccount,ToAccount,Amount,Currency,Type,Timestamp,Status",
            normalized);
    }

    [Fact]
    public async Task ExportTransactions_FormatCsv_WithTransactions_ReturnsOneDataRowAlignedWithListing()
    {
        var depositResponse = await _client.PostAsJsonAsync("/transactions", new
        {
            fromAccount = "ACC-CSV1",
            toAccount = "ACC-CSV2",
            amount = 42.50m,
            currency = "USD",
            type = "Transfer"
        });
        Assert.Equal(HttpStatusCode.Created, depositResponse.StatusCode);
        var created = await depositResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = Guid.Parse(created.GetProperty("id").GetString()!);

        var csvResponse = await _client.GetAsync("/transactions/export?format=csv");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        var body = await csvResponse.Content.ReadAsStringAsync();
        var normalized = body.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
        var lines = normalized.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("Id,FromAccount,ToAccount,Amount,Currency,Type,Timestamp,Status", lines[0]);
        Assert.StartsWith($"{id},", lines[1], StringComparison.Ordinal);
        Assert.Contains(",USD,Transfer,", lines[1], StringComparison.Ordinal);
        Assert.EndsWith(",Completed", lines[1], StringComparison.Ordinal);
        Assert.Contains("ACC-CSV1,", lines[1], StringComparison.Ordinal);
        Assert.Contains("ACC-CSV2,", lines[1], StringComparison.Ordinal);

        var listResponse = await _client.GetAsync("/transactions");
        var list = await listResponse.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(id.ToString(), list![0].GetProperty("id").GetString());
    }
}
