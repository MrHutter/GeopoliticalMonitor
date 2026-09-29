using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class SqliteAccentThemePreferenceStoreTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public SqliteAccentThemePreferenceStoreTests()
    {
        databasePath = Path.Combine(temporaryDirectory, "geopolitical-monitor-test.db");
    }

    [Fact]
    public async Task ReturnsOrangeByDefaultWhenNoPreferenceIsStored()
    {
        var service = await CreateMigratedServiceAsync();

        var accentTheme = await service.GetAccentThemeAsync();

        Assert.Equal(AccentTheme.Orange, accentTheme);
    }

    [Theory]
    [InlineData(AccentTheme.Orange)]
    [InlineData(AccentTheme.Blue)]
    [InlineData(AccentTheme.Red)]
    [InlineData(AccentTheme.Green)]
    [InlineData(AccentTheme.Yellow)]
    public async Task SavesAccentTheme(AccentTheme accentTheme)
    {
        var service = await CreateMigratedServiceAsync();

        await service.SaveAccentThemeAsync(accentTheme);

        Assert.Equal(accentTheme, await service.GetAccentThemeAsync());
    }

    [Fact]
    public async Task UpdatesAccentThemeWithLatestValue()
    {
        var service = await CreateMigratedServiceAsync();

        await service.SaveAccentThemeAsync(AccentTheme.Orange);
        await service.SaveAccentThemeAsync(AccentTheme.Blue);
        await service.SaveAccentThemeAsync(AccentTheme.Red);
        await service.SaveAccentThemeAsync(AccentTheme.Green);
        await service.SaveAccentThemeAsync(AccentTheme.Yellow);
        await service.SaveAccentThemeAsync(AccentTheme.Orange);

        Assert.Equal(AccentTheme.Orange, await service.GetAccentThemeAsync());
    }

    [Fact]
    public async Task PersistsAcrossDbContextInstances()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        var firstService = CreateService(initializer);
        await firstService.SaveAccentThemeAsync(AccentTheme.Blue);

        var secondInitializer = new LocalDatabaseInitializer(databasePath);
        var secondService = CreateService(secondInitializer);

        Assert.Equal(AccentTheme.Blue, await secondService.GetAccentThemeAsync());
    }

    [Fact]
    public async Task AppliesMigrationAndCreatesPreferenceSchema()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);

        await initializer.InitializeAsync();

        Assert.True(File.Exists(databasePath));
        await using var connection = new SqliteConnection(SqliteConnectionStringFactory.Create(databasePath));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'ApplicationPreferences';";
        var tableName = await command.ExecuteScalarAsync();
        Assert.Equal("ApplicationPreferences", tableName);

        var service = CreateService(initializer);
        await service.SaveAccentThemeAsync(AccentTheme.Blue);
        Assert.Equal(AccentTheme.Blue, await service.GetAccentThemeAsync());
    }

    [Fact]
    public async Task StoredInvalidAccentValueIsRejected()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        await using (var dbContext = initializer.CreateDbContext())
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO ApplicationPreferences (Key, Value, UpdatedAt) VALUES ({0}, {1}, {2})",
                AccentThemePreferenceService.PreferenceKey,
                "Purple",
                DateTimeOffset.UtcNow);
        }

        var service = CreateService(initializer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAccentThemeAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private async Task<AccentThemePreferenceService> CreateMigratedServiceAsync()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        return CreateService(initializer);
    }

    private static AccentThemePreferenceService CreateService(LocalDatabaseInitializer initializer)
    {
        var store = new SqliteAccentThemePreferenceStore(initializer.CreateDbContext);
        return new AccentThemePreferenceService(store);
    }
}
