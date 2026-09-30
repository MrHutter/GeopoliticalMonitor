using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class LocalDatabaseInitializer(string databasePath)
{
    public string DatabasePath { get; } = databasePath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var dataDirectory = Path.GetDirectoryName(DatabasePath)
            ?? throw new InvalidOperationException("The database directory could not be resolved.");
        Directory.CreateDirectory(dataDirectory);

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await EventSeverityBackfill.RunAsync(dbContext, cancellationToken).ConfigureAwait(false);
    }

    public GeopoliticalMonitorDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GeopoliticalMonitorDbContext>()
            .UseSqlite(SqliteConnectionStringFactory.Create(DatabasePath))
            .Options;

        return new GeopoliticalMonitorDbContext(options);
    }
}
