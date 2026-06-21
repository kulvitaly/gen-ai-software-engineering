using Application.Tickets;
using Infrastructure.Notifications;
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
        string? telegramBotToken = null,
        string connectionString = "Data Source=customer-support.db")
    {
        services.AddSingleton<ISqliteConnectionFactory>(_ => new SqliteConnectionFactory(connectionString));
        services.AddSingleton<ITelegramNotifier>(_ => new TelegramNotifier(telegramBotToken));
        services.AddScoped<ITicketRepository, SqliteTicketRepository>();

        return services;
    }
}
