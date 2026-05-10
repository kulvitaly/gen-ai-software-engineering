namespace TransactionApi.Tests.Fixtures;

/// <summary>
/// A unique shared in-memory SQLite database name per test class (via <see cref="Microsoft.Data.Sqlite"/> cache=shared).
/// </summary>
public sealed class TemporarySqliteDatabase : IAsyncLifetime
{
    public string ConnectionString { get; }

    public TemporarySqliteDatabase()
    {
        ConnectionString = $"Data Source=test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;
}
