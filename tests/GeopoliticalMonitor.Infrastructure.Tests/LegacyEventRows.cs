using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Tests;

/// <summary>
/// Inserts Event and SourceItem rows using only the columns that existed before the severity migration,
/// so tests can seed databases migrated to an earlier schema without the current EF model.
/// </summary>
internal static class LegacyEventRows
{
    public static Task InsertEventAsync(GeopoliticalMonitorDbContext db, Event geopoliticalEvent)
        => InsertEventAsync(
            db,
            geopoliticalEvent.Id.Value,
            geopoliticalEvent.Title,
            geopoliticalEvent.Summary,
            geopoliticalEvent.Category.ToString(),
            geopoliticalEvent.Status.ToString(),
            geopoliticalEvent.StartedAtUtc,
            geopoliticalEvent.LastUpdatedAtUtc,
            geopoliticalEvent.Location.Latitude,
            geopoliticalEvent.Location.Longitude,
            geopoliticalEvent.Location.DisplayName,
            geopoliticalEvent.Location.CountryCode,
            geopoliticalEvent.Location.Region,
            geopoliticalEvent.CreatedAtUtc,
            geopoliticalEvent.UpdatedAtUtc);

    public static Task InsertEventAsync(
        GeopoliticalMonitorDbContext db,
        string id,
        string title,
        string summary,
        string category,
        string status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset lastUpdatedAtUtc,
        double latitude,
        double longitude,
        string locationDisplayName,
        string? countryCode,
        string? region,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
        => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Events" ("Id", "Title", "Summary", "Category", "Status", "StartedAtUtc", "LastUpdatedAtUtc", "Latitude", "Longitude", "LocationDisplayName", "CountryCode", "Region", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({id}, {title}, {summary}, {category}, {status}, {startedAtUtc}, {lastUpdatedAtUtc}, {latitude}, {longitude}, {locationDisplayName}, {countryCode}, {region}, {createdAtUtc}, {updatedAtUtc})
            """);

    public static async Task InsertSourceForEventAsync(
        GeopoliticalMonitorDbContext db,
        string eventId,
        string sourceId,
        string sourceType,
        string? externalId,
        string text,
        string? url,
        DateTimeOffset publishedAtUtc)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SourceItems" ("Id", "SourceType", "ExternalId", "Title", "Text", "Url", "AuthorDisplayName", "AuthorHandle", "PublishedAtUtc", "RetrievedAtUtc", "Latitude", "Longitude", "LocationDisplayName", "CountryCode", "Region", "CreatedAtUtc")
            VALUES ({sourceId}, {sourceType}, {externalId}, {null as string}, {text}, {url}, {null as string}, {null as string}, {publishedAtUtc}, {publishedAtUtc}, {null as double?}, {null as double?}, {null as string}, {null as string}, {null as string}, {publishedAtUtc})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "EventSourceAssociations" ("EventId", "SourceItemId", "CreatedAtUtc")
            VALUES ({eventId}, {sourceId}, {publishedAtUtc})
            """);
    }
}
