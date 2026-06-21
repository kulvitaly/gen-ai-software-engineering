using Microsoft.Data.Sqlite;

namespace Infrastructure.Persistence;

public sealed class SqliteConnectionFactory(string connectionString) : ISqliteConnectionFactory
{
    public ValueTask<SqliteConnection> OpenConnection(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(connectionString);
        return Open(connection, cancellationToken);
    }

    private static async ValueTask<SqliteConnection> Open(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA busy_timeout = 5000;
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}
