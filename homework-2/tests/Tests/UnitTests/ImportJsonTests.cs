using Application.Common;
using Application.Tickets;
using Application.Tickets.Import;

namespace Tests;

public sealed class ImportJsonTests
{
    [Fact]
    public void Parse_WithArrayOfTickets_ReturnsRecords()
    {
        // Arrange
        var parser = new JsonTicketImportParser();

        // Act
        var result = parser.Parse(ImportFixtures.ValidJsonArray());

        // Assert
        Assert.Equal(1, result.TotalRecords);
        Assert.Single(result.Records);
        Assert.Empty(result.FailedRecords);
    }

    [Fact]
    public void Parse_WithWrapperObject_ReturnsRecords()
    {
        // Arrange
        var parser = new JsonTicketImportParser();
        var json = $$"""
        {
          "tickets": {{ImportFixtures.ValidJsonArray()}}
        }
        """;

        // Act
        var result = parser.Parse(json);

        // Assert
        Assert.Equal(1, result.TotalRecords);
        Assert.Single(result.Records);
    }

    [Fact]
    public void Parse_WithInvalidSchema_ReturnsFileError()
    {
        // Arrange
        var parser = new JsonTicketImportParser();

        // Act
        var result = parser.Parse("""{ "items": [] }""");

        // Assert
        var error = Assert.Single(result.FailedRecords);
        Assert.Equal(0, error.RecordNumber);
        Assert.Contains("tickets array", error.Errors[0]);
    }

    [Fact]
    public void Parse_WithEmptyArray_ReturnsEmptySummary()
    {
        // Arrange
        var parser = new JsonTicketImportParser();

        // Act
        var result = parser.Parse("[]");

        // Assert
        Assert.Equal(0, result.TotalRecords);
        Assert.Empty(result.Records);
        Assert.Empty(result.FailedRecords);
    }

    [Fact]
    public void Parse_WithMalformedJson_ReturnsFileError()
    {
        // Arrange
        var parser = new JsonTicketImportParser();

        // Act
        var result = parser.Parse("""[{ "customer_id": "customer-1" """);

        // Assert
        var error = Assert.Single(result.FailedRecords);
        Assert.Equal(0, error.RecordNumber);
        Assert.Contains("malformed", error.Errors[0], StringComparison.OrdinalIgnoreCase);
    }
}
