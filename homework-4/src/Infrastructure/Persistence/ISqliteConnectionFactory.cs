using Microsoft.Data.Sqlite;

namespace Infrastructure.Persistence;

public interface ISqliteConnectionFactory
{
    ValueTask<SqliteConnection> OpenConnection(CancellationToken cancellationToken = default);
}
