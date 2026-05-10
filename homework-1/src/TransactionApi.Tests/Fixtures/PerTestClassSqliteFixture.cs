using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransactionApi.Infrastructure;

namespace TransactionApi.Tests.Fixtures;

/// <summary>
/// One isolated shared in-memory SQLite database and <see cref="CustomWebApplicationFactory"/> per test class (use with xUnit <c>IClassFixture&lt;PerTestClassSqliteFixture&gt;</c>).
/// </summary>
public sealed class PerTestClassSqliteFixture : IAsyncLifetime
{
    private TemporarySqliteDatabase? _database;

    public CustomWebApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _database = new TemporarySqliteDatabase();
        await _database.InitializeAsync();
        Factory = new CustomWebApplicationFactory(_database.ConnectionString);
    }

    /// <summary>
    /// Removes all rows so each test method starts from a clean schema (same in-memory database for the class).
    /// </summary>
    public async Task ClearAllDataAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Transactions");
    }

    public async Task DisposeAsync()
    {
        Factory.Dispose();
        if (_database != null)
            await _database.DisposeAsync();
    }
}
