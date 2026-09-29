using Microsoft.Data.Sqlite;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public static class SqliteConnectionStringFactory
{
    public static string Create(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false,
        };

        return connectionStringBuilder.ToString();
    }
}