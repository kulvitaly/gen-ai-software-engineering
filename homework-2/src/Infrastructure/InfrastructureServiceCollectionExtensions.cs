using Application.Tickets;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class InfrastructureAssemblyMarker
{
    public const string Name = "Infrastructure";
}

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString = "Data Source=customer-support.db")
    {
        services.AddSingleton<ISqliteConnectionFactory>(_ => new SqliteConnectionFactory(connectionString));
        services.AddScoped<ITicketRepository, SqliteTicketRepository>();

        return services;
    }
}
