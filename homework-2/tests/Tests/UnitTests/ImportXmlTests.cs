using Application.Tickets;
using Application.Tickets.Import;

namespace Tests;

public sealed class ImportXmlTests
{
    [Fact]
    public void Parse_WithValidTicketList_ReturnsRecords()
    {
        // Arrange
        var parser = new XmlTicketImportParser();

        // Act
        var result = parser.Parse(ImportFixtures.ValidXml());

        // Assert
        Assert.Equal(1, result.TotalRecords);
        Assert.Single(result.Records);
        Assert.Empty(result.FailedRecords);
    }

    [Fact]
    public void Parse_WithMalformedXml_ReturnsFileError()
    {
        // Arrange
        var parser = new XmlTicketImportParser();

        // Act
        var result = parser.Parse("<tickets><ticket></tickets>");

        // Assert
        var error = Assert.Single(result.FailedRecords);
        Assert.Equal(0, error.RecordNumber);
        Assert.Contains("malformed", error.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WithWrongRoot_ReturnsFileError()
    {
        // Arrange
        var parser = new XmlTicketImportParser();

        // Act
        var result = parser.Parse("<items></items>");

        // Assert
        var error = Assert.Single(result.FailedRecords);
        Assert.Equal(0, error.RecordNumber);
        Assert.Contains("root element", error.Errors[0]);
    }

    [Fact]
    public void Parse_WithNoTicketElements_ReturnsFileError()
    {
        // Arrange
        var parser = new XmlTicketImportParser();

        // Act
        var result = parser.Parse("<tickets></tickets>");

        // Assert
        var error = Assert.Single(result.FailedRecords);
        Assert.Equal(0, error.RecordNumber);
        Assert.Contains("ticket element", error.Errors[0]);
    }

    [Fact]
    public async Task Import_WithInvalidTicket_ReturnsFailedRecord()
    {
        // Arrange
        var repository = new ImportTestRepository();
        var handler = new ImportTicketsCommandHandler(
            [new XmlTicketImportParser()],
            repository,
            new ImportTestClock(new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero)));
        var xml = """
        <tickets>
          <ticket>
            <customer_id>customer-1</customer_id>
            <customer_email>bad-email</customer_email>
            <customer_name>Ada Lovelace</customer_name>
            <subject>Cannot access account</subject>
            <description>I cannot access my customer account after resetting my password.</description>
            <category>account_access</category>
            <priority>high</priority>
            <status>new</status>
            <metadata><source>web_form</source><device_type>desktop</device_type></metadata>
          </ticket>
        </tickets>
        """;

        // Act
        var result = await handler.Handle(new ImportTicketsCommand(TicketImportFormat.Xml, xml), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Total);
        Assert.Equal(0, result.Value.Successful);
        Assert.Single(result.Value.Failed);
        Assert.Empty(repository.Tickets);
    }
}
