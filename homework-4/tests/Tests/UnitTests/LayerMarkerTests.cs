using Application;
using Domain;
using Infrastructure;

namespace Tests;

public sealed class LayerMarkerTests
{
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
}
