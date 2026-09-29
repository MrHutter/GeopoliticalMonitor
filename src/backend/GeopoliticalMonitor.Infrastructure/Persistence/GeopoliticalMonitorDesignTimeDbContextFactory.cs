using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class GeopoliticalMonitorDesignTimeDbContextFactory : IDesignTimeDbContextFactory<GeopoliticalMonitorDbContext>
{
    public GeopoliticalMonitorDbContext CreateDbContext(string[] args)
    {
        var databasePath = new LocalApplicationDataPathProvider().GetDatabasePath();
        var options = new DbContextOptionsBuilder<GeopoliticalMonitorDbContext>()
            .UseSqlite(SqliteConnectionStringFactory.Create(databasePath))
            .Options;

        return new GeopoliticalMonitorDbContext(options);
    }
}
