using Application.Common;
using Application.Tickets;
using Application.Tickets.Import;

namespace Tests;

public sealed class ImportCsvTests
{
    [Fact]
    public void Parse_WithValidMultiRowCsv_ReturnsRecords()
    {
        // Arrange
        var parser = new CsvTicketImportParser();

        // Act
        var result = parser.Parse(ImportFixtures.ValidCsv());

        // Assert
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(2, result.Records.Count);
        Assert.Empty(result.FailedRecords);
    }

    [Fact]
    public void Parse_WhenHeaderIsMissing_ReturnsFileError()
    {
        // Arrange
        var parser = new CsvTicketImportParser();
        var csv = "customer_id,customer_email" + Environment.NewLine + "customer-1,ada@example.com";

        // Act
        var result = parser.Parse(csv);

        // Assert
        var error = Assert.Single(result.FailedRecords);
        Assert.Equal(0, error.RecordNumber);
        Assert.Contains("missing required headers", error.Errors[0]);
    }

    [Fact]
    public async Task Import_WhenCsvContainsBadEmail_ReturnsFailedRecord()
    {
        // Arrange
        var repository = new ImportTestRepository();
        var handler = Handler(repository);
        var csv = string.Join(
            Environment.NewLine,
            ImportFixtures.CsvHeader,
            "customer-1,not-an-email,Ada Lovelace,Cannot access account,I cannot access my customer account after resetting my password.,account_access,high,new,account;login,web_form,Edge,desktop,");

        // Act
        var result = await handler.Handle(new ImportTicketsCommand(TicketImportFormat.Csv, csv), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Total);
        Assert.Equal(0, result.Value.Successful);
        var failed = Assert.Single(result.Value.Failed);
        Assert.Equal(1, failed.RecordNumber);
        Assert.Contains(failed.Errors, error => error.Contains("valid email", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Tickets);
    }

    [Fact]
    public void Parse_WithQuotedFields_PreservesCommas()
    {
        // Arrange
        var parser = new CsvTicketImportParser();
        var csv = string.Join(
            Environment.NewLine,
            ImportFixtures.CsvHeader,
            "customer-1,ada@example.com,Ada Lovelace,\"Cannot access, account\",\"I cannot access my customer account, after resetting my password.\",account_access,high,new,\"account;login\",web_form,Edge,desktop,");

        // Act
        var result = parser.Parse(csv);

        // Assert
        var record = Assert.Single(result.Records);
        Assert.Equal("Cannot access, account", record.Subject);
        Assert.Equal("I cannot access my customer account, after resetting my password.", record.Description);
    }

    [Fact]
    public async Task Import_WithMixedValidAndInvalidRows_ReturnsPartialSuccess()
    {
        // Arrange
        var repository = new ImportTestRepository();
        var handler = Handler(repository);
        var csv = string.Join(
            Environment.NewLine,
            ImportFixtures.CsvHeader,
            "customer-1,ada@example.com,Ada Lovelace,Cannot access account,I cannot access my customer account after resetting my password.,account_access,high,new,account;login,web_form,Edge,desktop,",
            "customer-2,bad-email,Grace Hopper,Billing invoice question,I need help understanding the latest annual invoice.,billing_question,medium,new,billing;invoice,email,Firefox,desktop,");

        // Act
        var result = await handler.Handle(new ImportTicketsCommand(TicketImportFormat.Csv, csv), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Total);
        Assert.Equal(1, result.Value.Successful);
        Assert.Single(result.Value.Failed);
        Assert.Single(repository.Tickets);
    }

    [Fact]
    public void Parse_WhenCsvIsEmpty_ReturnsFileError()
    {
        // Arrange
        var parser = new CsvTicketImportParser();

        // Act
        var result = parser.Parse(" ");

        // Assert
        var error = Assert.Single(result.FailedRecords);
        Assert.Equal(0, error.RecordNumber);
        Assert.Contains("empty", error.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    private static ImportTicketsCommandHandler Handler(ImportTestRepository repository)
    {
        return new ImportTicketsCommandHandler(
            [new CsvTicketImportParser()],
            repository,
            new ImportTestClock(new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero)));
    }
}
