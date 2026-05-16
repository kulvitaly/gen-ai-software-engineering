using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class InfrastructureAssemblyMarker
{
    public const string Name = "Infrastructure";
}

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        return services;
    }
}
