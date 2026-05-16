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
        Assert.Equal("Domain", DomainAssemblyMarker.Name);
        Assert.Equal("Application", ApplicationAssemblyMarker.Name);
        Assert.Equal("Infrastructure", InfrastructureAssemblyMarker.Name);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOkStatus()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
        Assert.Equal("CustomerSupportSystem", body.Service);
    }

    private sealed record HealthResponse(string Status, string Service);
}
