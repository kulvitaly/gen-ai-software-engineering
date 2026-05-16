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
        return connection;
    }
}
