using System.Net;
using System.Net.Http.Json;
using Application;
using Domain;
using Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tests;

public sealed class Phase0SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public Phase0SmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void LayerMarkers_AreAvailableToTheTestProject()
    {
        // Arrange
        var expectedDomainName = "Domain";
        var expectedApplicationName = "Application";
        var expectedInfrastructureName = "Infrastructure";

        // Act
        var domainName = DomainAssemblyMarker.Name;
        var applicationName = ApplicationAssemblyMarker.Name;
        var infrastructureName = InfrastructureAssemblyMarker.Name;

        // Assert
        Assert.Equal(expectedDomainName, domainName);
        Assert.Equal(expectedApplicationName, applicationName);
        Assert.Equal(expectedInfrastructureName, infrastructureName);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOkStatus()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
        Assert.Equal("CustomerSupportSystem", body.Service);
    }

    private sealed record HealthResponse(string Status, string Service);
}
