using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class DemoDataSeeder(EventCatalogService eventCatalogService)
{
    private static readonly DateTimeOffset BaseTime = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var item in DemoEvents())
        {
            await eventCatalogService.CreateEventAsync(item, cancellationToken).ConfigureAwait(false);
        }

        foreach (var item in DemoSources())
        {
            await eventCatalogService.CreateSourceItemAsync(item, cancellationToken).ConfigureAwait(false);
        }

        foreach (var (eventId, sourceId) in DemoAssociations())
        {
            await eventCatalogService.AttachSourceToEventAsync(new EventId(eventId), new SourceItemId(sourceId), cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<Event> DemoEvents() =>
    [
        CreateEvent("evt-demo-baltic-security", "Reported security activity near Baltic demo border area", "Demonstration event showing several source items supporting one normalized security occurrence.", EventCategory.Security, EventStatus.Developing, 57.4, 24.8, "Northern corridor", "DE", "Baltic demo region", -12),
        CreateEvent("evt-demo-red-sea-shipping", "Commercial shipping disruption reported in Red Sea demo lane", "Demonstration maritime-adjacent logistics event using local, fictional source material only.", EventCategory.Infrastructure, EventStatus.Active, 15.8, 41.9, "Southern transit lane", null, "Red Sea demo corridor", -28),
        CreateEvent("evt-demo-europe-diplomatic", "Diplomatic consultation announced after regional demo talks", "Demonstration political-diplomatic event for lower-urgency source grouping.", EventCategory.PoliticalDiplomatic, EventStatus.Resolved, 48.2, 16.3, "Capital district", "AT", "Central Europe demo region", -46),
        CreateEvent("evt-demo-east-asia-infrastructure", "Infrastructure disruption affects demo logistics hub", "Demonstration infrastructure event for non-conflict map markers and source distinction.", EventCategory.Infrastructure, EventStatus.Active, 35.6, 139.7, "Coastal logistics zone", "JP", "East Asia demo region", -60),
        CreateEvent("evt-demo-north-africa-civil", "Demonstration activity reported around civic district", "Demonstration civil-unrest event with fictional social and local media source items.", EventCategory.CivilUnrest, EventStatus.Unverified, 30.0, 31.2, "Civic district", "EG", "North Africa demo region", -120),
        CreateEvent("evt-demo-humanitarian-crossing", "Humanitarian access concern near demo crossing point", "Demonstration humanitarian event with official-style and social source material.", EventCategory.Humanitarian, EventStatus.Developing, 32.1, 36.0, "Border crossing area", null, "Middle East demo region", -180),
    ];

    private static IReadOnlyList<SourceItem> DemoSources() =>
    [
        CreateSource("src-demo-baltic-x-1", SourceType.XPost, "Demo field watcher", "Demo-only post referencing movement near a northern border corridor.", "Demo Field Watcher", "@demo_field_watch", -12, 57.4, 24.8, "Northern corridor"),
        CreateSource("src-demo-baltic-news-1", SourceType.NewsArticle, "Regional demo brief", "Fictional regional brief describing similar security activity near the same area.", "Regional Demo Desk", null, -8, 57.2, 24.7, "Northern corridor"),
        CreateSource("src-demo-baltic-official-1", SourceType.OfficialStatement, "Mock ministry note", "Demonstration official-style note acknowledging a routine security review.", "Demo Public Affairs", null, -2, null, null, null),
        CreateSource("src-demo-redsea-news-1", SourceType.NewsArticle, "Demo maritime desk", "Fictional shipping desk report about route planning disruption.", "Demo Maritime Desk", null, -28, 15.8, 41.9, "Southern transit lane"),
        CreateSource("src-demo-redsea-x-1", SourceType.XPost, "Demo shipping observer", "Mock social post mentioning altered commercial route planning.", "Demo Shipping Observer", "@demo_shipping", -22, null, null, null),
        CreateSource("src-demo-redsea-dataset-1", SourceType.DatasetRecord, "Mock route dataset", "Demonstration-only dataset row for a transit-lane delay.", null, null, -18, null, null, null),
        CreateSource("src-demo-europe-official-1", SourceType.OfficialStatement, "Mock foreign office schedule", "Fictional schedule note for a diplomatic consultation.", "Demo Foreign Office", null, -46, 48.2, 16.3, "Capital district"),
        CreateSource("src-demo-europe-news-1", SourceType.NewsArticle, "Demo diplomatic wire", "Demonstration wire-style summary of regional talks.", "Diplomatic Wire Demo", null, -42, null, null, null),
        CreateSource("src-demo-asia-dataset-1", SourceType.DatasetRecord, "Mock logistics dataset", "Operational placeholder row for a logistics disruption.", null, null, -60, 35.6, 139.7, "Coastal logistics zone"),
        CreateSource("src-demo-asia-news-1", SourceType.NewsArticle, "Demo local business brief", "Fictional local report about port and rail delays.", "Demo Business Brief", null, -54, null, null, null),
        CreateSource("src-demo-africa-x-1", SourceType.XPost, "Demo civic observer", "Demo social post mentioning gathering near district offices.", "Demo Civic Observer", "@demo_civic", -120, 30.0, 31.2, "Civic district"),
        CreateSource("src-demo-africa-news-1", SourceType.NewsArticle, "Mock city desk", "Fictional city desk note about civic district demonstration activity.", "Mock City Desk", null, -105, null, null, null),
        CreateSource("src-demo-humanitarian-official-1", SourceType.OfficialStatement, "Demo relief office", "Official-style demonstration note about access concerns.", "Demo Relief Office", null, -180, 32.1, 36.0, "Border crossing area"),
        CreateSource("src-demo-humanitarian-x-1", SourceType.XPost, "Demo humanitarian monitor", "Mock post referencing crossing delay and aid access concerns.", "Demo Humanitarian Monitor", "@demo_aid_watch", -170, null, null, null),
    ];

    private static IReadOnlyList<(string EventId, string SourceId)> DemoAssociations() =>
    [
        ("evt-demo-baltic-security", "src-demo-baltic-x-1"),
        ("evt-demo-baltic-security", "src-demo-baltic-news-1"),
        ("evt-demo-baltic-security", "src-demo-baltic-official-1"),
        ("evt-demo-red-sea-shipping", "src-demo-redsea-news-1"),
        ("evt-demo-red-sea-shipping", "src-demo-redsea-x-1"),
        ("evt-demo-red-sea-shipping", "src-demo-redsea-dataset-1"),
        ("evt-demo-europe-diplomatic", "src-demo-europe-official-1"),
        ("evt-demo-europe-diplomatic", "src-demo-europe-news-1"),
        ("evt-demo-east-asia-infrastructure", "src-demo-asia-dataset-1"),
        ("evt-demo-east-asia-infrastructure", "src-demo-asia-news-1"),
        ("evt-demo-north-africa-civil", "src-demo-africa-x-1"),
        ("evt-demo-north-africa-civil", "src-demo-africa-news-1"),
        ("evt-demo-humanitarian-crossing", "src-demo-humanitarian-official-1"),
        ("evt-demo-humanitarian-crossing", "src-demo-humanitarian-x-1"),
    ];

    private static Event CreateEvent(string id, string title, string summary, EventCategory category, EventStatus status, double latitude, double longitude, string displayName, string? countryCode, string region, int minutesOffset)
    {
        var timestamp = BaseTime.AddMinutes(minutesOffset);
        if (!DemoEventSeverities.TryGetAssessment(id, out var severity))
        {
            throw new InvalidOperationException($"Demo event '{id}' has no severity assignment.");
        }

        return Event.Create(new EventId(id), title, summary, category, status, timestamp, timestamp.AddMinutes(6), new Location(latitude, longitude, displayName, countryCode, region), BaseTime.AddHours(-6), timestamp.AddMinutes(6), severity!, DemoEventSeverities.Provenance);
    }

    private static SourceItem CreateSource(string id, SourceType type, string? title, string text, string? authorDisplayName, string? authorHandle, int minutesOffset, double? latitude, double? longitude, string? displayName)
    {
        Location? location = null;
        if (latitude is not null && longitude is not null && displayName is not null)
        {
            location = new Location(latitude.Value, longitude.Value, displayName);
        }

        var timestamp = BaseTime.AddMinutes(minutesOffset);
        return SourceItem.Create(new SourceItemId(id), type, id, title, text, null, authorDisplayName, authorHandle, timestamp, timestamp.AddMinutes(3), location, BaseTime.AddHours(-6));
    }
}