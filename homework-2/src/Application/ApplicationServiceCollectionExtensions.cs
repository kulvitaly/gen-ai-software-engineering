using Application.Common;
using Application.Tickets.Import;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class ApplicationAssemblyMarker
{
    public const string Name = "Application";
}

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(ApplicationAssemblyMarker).Assembly));
        services.AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITicketImportParser, CsvTicketImportParser>();
        services.AddSingleton<ITicketImportParser, JsonTicketImportParser>();
        services.AddSingleton<ITicketImportParser, XmlTicketImportParser>();

        return services;
    }
}
