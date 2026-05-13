using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransactionApi.Infrastructure;

namespace TransactionApi.Tests.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CustomWebApplicationFactory(string sqliteConnectionString)
    {
        _connectionString = sqliteConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType == typeof(SqliteConnection)).ToList())
                services.Remove(d);

            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<TransactionDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddSingleton(_ =>
            {
                var connection = new SqliteConnection(_connectionString);
                connection.Open();
                return connection;
            });

            services.AddDbContext<TransactionDbContext>(options =>
                options.UseSqlite(_connectionString));
        });

        builder.UseEnvironment("Testing");
    }
}
