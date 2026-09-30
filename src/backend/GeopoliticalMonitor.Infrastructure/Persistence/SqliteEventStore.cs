using GeopoliticalMonitor.Domain.Alerts;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Application.ReliefWeb;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class SqliteEventStore(Func<GeopoliticalMonitorDbContext> createDbContext) : IEventStore, IGdacsEarthquakeStore, IReliefWebSourceStore
{
    public async Task<IReadOnlyList<Event>> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var entities = await dbContext.Events
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.OrderByDescending(item => item.LastUpdatedAtUtc).Select(ToDomain).ToArray();
    }

    public async Task<Event?> GetEventByIdAsync(EventId eventId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var entity = await dbContext.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == eventId.Value, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<SourceItem>> GetSourcesForEventAsync(EventId eventId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var entities = await dbContext.EventSourceAssociations
            .AsNoTracking()
            .Where(item => item.EventId == eventId.Value)
            .Join(dbContext.SourceItems.AsNoTracking(), association => association.SourceItemId, source => source.Id, (_, source) => source)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.OrderByDescending(item => item.PublishedAtUtc).ThenBy(item => item.Id).Select(ToDomain).ToArray();
    }

    public async Task<IReadOnlyDictionary<EventId, int>> GetSourceCountsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var counts = await dbContext.EventSourceAssociations
            .AsNoTracking()
            .GroupBy(item => item.EventId)
            .Select(group => new { EventId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return counts.ToDictionary(item => new EventId(item.EventId), item => item.Count);
    }

    public async Task CreateEventAsync(Event geopoliticalEvent, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        if (await dbContext.Events.AnyAsync(item => item.Id == geopoliticalEvent.Id.Value, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        dbContext.Events.Add(ToEntity(geopoliticalEvent));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateSourceItemAsync(SourceItem sourceItem, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        if (await dbContext.SourceItems.AnyAsync(item => item.Id == sourceItem.Id.Value, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        dbContext.SourceItems.Add(ToEntity(sourceItem));
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AttachSourceToEventAsync(EventId eventId, SourceItemId sourceItemId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var exists = await dbContext.EventSourceAssociations
            .AnyAsync(item => item.EventId == eventId.Value && item.SourceItemId == sourceItemId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        dbContext.EventSourceAssociations.Add(new EventSourceAssociationEntity
        {
            EventId = eventId.Value,
            SourceItemId = sourceItemId.Value,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SourceAttachmentStoreResult> AttachSourceToEventAsync(
        EventId eventId,
        SourceItem sourceItem,
        CancellationToken cancellationToken = default)
    {
        var normalizedUrl = sourceItem.Url?.AbsoluteUri
            ?? throw new ArgumentException("Source URL is required for identity-based attachment.", nameof(sourceItem));

        await using var dbContext = createDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        if (!await dbContext.Events.AnyAsync(item => item.Id == eventId.Value, cancellationToken).ConfigureAwait(false))
        {
            return new SourceAttachmentStoreResult(SourceAttachmentStoreDisposition.EventNotFound);
        }

        var sourceType = sourceItem.SourceType.ToString();
        var existingEntity = await dbContext.SourceItems
            .SingleOrDefaultAsync(
                item => item.SourceType == sourceType && item.Url == normalizedUrl,
                cancellationToken)
            .ConfigureAwait(false);

        var sourceWasCreated = existingEntity is null;
        var persistedSource = sourceWasCreated ? sourceItem : ToDomain(existingEntity!);
        if (sourceWasCreated)
        {
            dbContext.SourceItems.Add(ToEntity(sourceItem));
        }

        var associationExists = await dbContext.EventSourceAssociations
            .AnyAsync(
                item => item.EventId == eventId.Value && item.SourceItemId == persistedSource.Id.Value,
                cancellationToken)
            .ConfigureAwait(false);

        if (associationExists)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SourceAttachmentStoreResult(SourceAttachmentStoreDisposition.AlreadyAttached, persistedSource);
        }

        dbContext.EventSourceAssociations.Add(new EventSourceAssociationEntity
        {
            EventId = eventId.Value,
            SourceItemId = persistedSource.Id.Value,
            CreatedAtUtc = sourceItem.CreatedAtUtc,
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new SourceAttachmentStoreResult(
            sourceWasCreated
                ? SourceAttachmentStoreDisposition.CreatedAndAttached
                : SourceAttachmentStoreDisposition.ExistingSourceAttached,
            persistedSource);
    }

    public async Task<ReliefWebAttachmentStoreResult> AttachReliefWebSourceAsync(
        EventId eventId,
        SourceItem sourceItem,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        if (!await dbContext.Events.AnyAsync(item => item.Id == eventId.Value, cancellationToken).ConfigureAwait(false))
        {
            return new ReliefWebAttachmentStoreResult(ReliefWebAttachmentStoreDisposition.EventNotFound);
        }

        var expectedExternalId = sourceItem.ExternalId;
        var existingWithId = await dbContext.SourceItems
            .SingleOrDefaultAsync(item => item.Id == sourceItem.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        var externalIdCollision = await dbContext.SourceItems
            .AnyAsync(
                item => item.ExternalId == expectedExternalId && item.Id != sourceItem.Id.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (externalIdCollision || (existingWithId is not null && !IsExpectedReliefWebSource(existingWithId, sourceItem)))
        {
            return new ReliefWebAttachmentStoreResult(ReliefWebAttachmentStoreDisposition.Conflict);
        }

        var sourceWasCreated = existingWithId is null;
        var persistedSource = sourceWasCreated ? sourceItem : ToDomain(existingWithId!);
        if (sourceWasCreated)
        {
            dbContext.SourceItems.Add(ToEntity(sourceItem));
        }

        var associationExists = await dbContext.EventSourceAssociations
            .AnyAsync(
                item => item.EventId == eventId.Value && item.SourceItemId == persistedSource.Id.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (associationExists)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ReliefWebAttachmentStoreResult(ReliefWebAttachmentStoreDisposition.AlreadyAttached, persistedSource);
        }

        dbContext.EventSourceAssociations.Add(new EventSourceAssociationEntity
        {
            EventId = eventId.Value,
            SourceItemId = persistedSource.Id.Value,
            CreatedAtUtc = sourceItem.CreatedAtUtc,
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new ReliefWebAttachmentStoreResult(
            sourceWasCreated
                ? ReliefWebAttachmentStoreDisposition.CreatedAndAttached
                : ReliefWebAttachmentStoreDisposition.ExistingSourceAttached,
            persistedSource);
    }

    public async Task<GdacsEarthquakePersistenceResult> PersistGdacsEarthquakesAsync(
        IReadOnlyList<GdacsEarthquakeImport> imports,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (imports.Count == 0)
        {
            return new GdacsEarthquakePersistenceResult(0, 0, 0, 0, 0, 0, [], 0);
        }

        await using var dbContext = createDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var eventIds = imports.Select(item => item.Event.Id.Value).Distinct().ToArray();
        var sourceIds = imports.Select(item => item.Source.Id.Value).Distinct().ToArray();
        var events = await dbContext.Events
            .Where(item => eventIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken)
            .ConfigureAwait(false);
        var sources = await dbContext.SourceItems
            .Where(item => sourceIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken)
            .ConfigureAwait(false);
        var ownershipSources = await dbContext.EventSourceAssociations
            .Where(item => eventIds.Contains(item.EventId))
            .Join(
                dbContext.SourceItems,
                association => association.SourceItemId,
                source => source.Id,
                (association, source) => new { association.EventId, Source = source })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var associations = (await dbContext.EventSourceAssociations
                .Where(item => eventIds.Contains(item.EventId) && sourceIds.Contains(item.SourceItemId))
                .Select(item => new { item.EventId, item.SourceItemId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Select(item => (item.EventId, item.SourceItemId))
            .ToHashSet();
        var monitoredRuleLinks = (await dbContext.AlertRules
                .AsNoTracking()
                .Where(item => item.IsEnabled && item.TriggerType == AlertTriggerType.GdacsEventMateriallyUpdated.ToString())
                .Join(
                    dbContext.WatchlistEvents.Where(item => eventIds.Contains(item.EventId)),
                    rule => rule.WatchlistId,
                    membership => membership.WatchlistId,
                    (rule, membership) => new { membership.EventId, RuleId = rule.Id })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .ToLookup(item => item.EventId, item => item.RuleId, StringComparer.Ordinal);

        var providerOwnedEvents = ownershipSources
            .Where(item => IsGdacsSource(item.Source, item.EventId))
            .Select(item => item.EventId)
            .ToHashSet(StringComparer.Ordinal);
        var newEvents = 0;
        var updatedEvents = 0;
        var newSources = 0;
        var updatedSources = 0;
        var unchangedRecords = 0;
        var conflicts = 0;
        var newAlerts = 0;
        var eventOutcomes = new List<GdacsEventPersistenceOutcome>(imports.Count);

        foreach (var import in imports)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var eventId = import.Event.Id.Value;
            var sourceId = import.Source.Id.Value;
            events.TryGetValue(eventId, out var eventEntity);
            sources.TryGetValue(sourceId, out var sourceEntity);

            if ((eventEntity is not null && !providerOwnedEvents.Contains(eventId))
                || (sourceEntity is not null && !IsExpectedGdacsSource(sourceEntity, import)))
            {
                conflicts++;
                eventOutcomes.Add(new GdacsEventPersistenceOutcome(
                    eventId,
                    import.Event.LastUpdatedAtUtc,
                    GdacsEventPersistenceDisposition.Conflict));
                continue;
            }

            var eventDecision = DecideEventUpdate(eventEntity, import.Event);
            var sourceDecision = DecideSourceUpdate(sourceEntity, import.Source);
            if (eventDecision == UpdateDecision.Conflict || sourceDecision == UpdateDecision.Conflict)
            {
                conflicts++;
                eventOutcomes.Add(new GdacsEventPersistenceOutcome(
                    eventId,
                    import.Event.LastUpdatedAtUtc,
                    GdacsEventPersistenceDisposition.Conflict));
                continue;
            }

            var changed = false;
            var eventOutcome = GdacsEventPersistenceDisposition.Unchanged;
            if (eventEntity is null)
            {
                eventEntity = ToEntity(import.Event);
                dbContext.Events.Add(eventEntity);
                events.Add(eventId, eventEntity);
                providerOwnedEvents.Add(eventId);
                newEvents++;
                changed = true;
                eventOutcome = GdacsEventPersistenceDisposition.Created;
            }
            else if (eventDecision == UpdateDecision.Update)
            {
                var materiallyChanged = !EventMateriallyMatches(eventEntity, import.Event);
                ApplyProviderEventUpdate(eventEntity, import.Event);
                updatedEvents++;
                changed = true;
                eventOutcome = materiallyChanged
                    ? GdacsEventPersistenceDisposition.MateriallyUpdated
                    : GdacsEventPersistenceDisposition.RevisionOnlyUpdated;

                if (materiallyChanged)
                {
                    foreach (var ruleId in monitoredRuleLinks[eventId])
                    {
                        var occurrence = AlertOccurrence.Create(
                            AlertId.CreateNew(),
                            new AlertRuleId(ruleId),
                            import.Event.Id,
                            import.Event.LastUpdatedAtUtc,
                            observedAtUtc);
                        dbContext.AlertOccurrences.Add(new AlertOccurrenceEntity
                        {
                            Id = occurrence.Id.Value,
                            AlertRuleId = occurrence.AlertRuleId.Value,
                            EventId = occurrence.EventId.Value,
                            ProviderRevisionAtUtc = occurrence.ProviderRevisionAtUtc,
                            TriggeredAtUtc = occurrence.TriggeredAtUtc,
                            ReadAtUtc = occurrence.ReadAtUtc,
                        });
                        newAlerts++;
                    }
                }
            }
            else if (eventDecision == UpdateDecision.SeverityOnly)
            {
                ApplySeverity(eventEntity, import.Event);
                updatedEvents++;
                changed = true;
                eventOutcome = GdacsEventPersistenceDisposition.SeverityOnlyUpdated;
            }

            if (sourceEntity is null)
            {
                sourceEntity = ToEntity(import.Source);
                dbContext.SourceItems.Add(sourceEntity);
                sources.Add(sourceId, sourceEntity);
                newSources++;
                changed = true;
            }
            else if (sourceDecision == UpdateDecision.Update)
            {
                ApplyProviderSourceUpdate(sourceEntity, import.Source);
                updatedSources++;
                changed = true;
            }

            if (associations.Add((eventId, sourceId)))
            {
                dbContext.EventSourceAssociations.Add(new EventSourceAssociationEntity
                {
                    EventId = eventId,
                    SourceItemId = sourceId,
                    CreatedAtUtc = import.Source.CreatedAtUtc,
                });
                changed = true;
            }

            if (!changed)
            {
                unchangedRecords++;
            }

            eventOutcomes.Add(new GdacsEventPersistenceOutcome(
                eventId,
                import.Event.LastUpdatedAtUtc,
                eventOutcome));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new GdacsEarthquakePersistenceResult(
            newEvents,
            updatedEvents,
            newSources,
            updatedSources,
            unchangedRecords,
            conflicts,
            eventOutcomes,
            newAlerts);
    }

    private static UpdateDecision DecideEventUpdate(EventEntity? existing, Event incoming)
    {
        if (existing is null)
        {
            return UpdateDecision.Create;
        }

        if (incoming.LastUpdatedAtUtc > existing.LastUpdatedAtUtc)
        {
            return UpdateDecision.Update;
        }

        if (incoming.LastUpdatedAtUtc < existing.LastUpdatedAtUtc)
        {
            return UpdateDecision.Unchanged;
        }

        if (!EventMateriallyMatches(existing, incoming))
        {
            return UpdateDecision.Conflict;
        }

        // Severity fields are derived, not provider data, so they never cause an equal-revision Conflict.
        // Only a row still carrying the legacy backfill default adopts the computed severity.
        return existing.SeverityRule == EventSeverityAssessment.LegacyDefaultRule && !SeverityMatches(existing, incoming)
            ? UpdateDecision.SeverityOnly
            : UpdateDecision.Unchanged;
    }

    private static UpdateDecision DecideSourceUpdate(SourceItemEntity? existing, SourceItem incoming)
    {
        if (existing is null)
        {
            return UpdateDecision.Create;
        }

        if (incoming.PublishedAtUtc > existing.PublishedAtUtc)
        {
            return UpdateDecision.Update;
        }

        if (incoming.PublishedAtUtc < existing.PublishedAtUtc)
        {
            return UpdateDecision.Unchanged;
        }

        return SourceMateriallyMatches(existing, incoming) ? UpdateDecision.Unchanged : UpdateDecision.Conflict;
    }

    private static bool IsGdacsSource(SourceItemEntity source, string eventId)
    {
        if (!eventId.StartsWith(GdacsRefreshService.EventIdPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var providerEventId = eventId[GdacsRefreshService.EventIdPrefix.Length..];
        return source.SourceType == SourceType.DatasetRecord.ToString()
            && source.ExternalId?.StartsWith($"{GdacsRefreshService.ExternalIdPrefix}{providerEventId}:", StringComparison.Ordinal) == true
            && IsOfficialGdacsUrl(source.Url);
    }

    private static bool IsExpectedGdacsSource(SourceItemEntity source, GdacsEarthquakeImport import)
    {
        return source.SourceType == SourceType.DatasetRecord.ToString()
            && source.ExternalId == import.Source.ExternalId
            && IsOfficialGdacsUrl(source.Url);
    }

    private static bool IsExpectedReliefWebSource(SourceItemEntity source, SourceItem incoming)
    {
        return source.SourceType == SourceType.Other.ToString()
            && source.ExternalId == incoming.ExternalId
            && IsOfficialReliefWebUrl(source.Url);
    }

    private static bool IsOfficialReliefWebUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && (string.Equals(uri.Host, "reliefweb.int", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "www.reliefweb.int", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOfficialGdacsUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, "www.gdacs.org", StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.AbsolutePath, "/report.aspx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EventMateriallyMatches(EventEntity existing, Event incoming)
    {
        return existing.Title == incoming.Title
            && existing.Summary == incoming.Summary
            && existing.StartedAtUtc == incoming.StartedAtUtc
            && existing.Latitude == incoming.Location.Latitude
            && existing.Longitude == incoming.Location.Longitude
            && existing.LocationDisplayName == incoming.Location.DisplayName
            && existing.CountryCode == incoming.Location.CountryCode
            && existing.Region == incoming.Location.Region;
    }

    private static bool SourceMateriallyMatches(SourceItemEntity existing, SourceItem incoming)
    {
        return existing.Title == incoming.Title
            && existing.Text == incoming.Text
            && existing.Url == incoming.Url?.AbsoluteUri
            && existing.AuthorDisplayName == incoming.AuthorDisplayName
            && existing.AuthorHandle == incoming.AuthorHandle
            && existing.Latitude == incoming.Location?.Latitude
            && existing.Longitude == incoming.Location?.Longitude
            && existing.LocationDisplayName == incoming.Location?.DisplayName
            && existing.CountryCode == incoming.Location?.CountryCode
            && existing.Region == incoming.Location?.Region;
    }

    private static void ApplyProviderEventUpdate(EventEntity existing, Event incoming)
    {
        existing.Title = incoming.Title;
        existing.Summary = incoming.Summary;
        existing.StartedAtUtc = incoming.StartedAtUtc;
        existing.LastUpdatedAtUtc = incoming.LastUpdatedAtUtc;
        existing.Latitude = incoming.Location.Latitude;
        existing.Longitude = incoming.Location.Longitude;
        existing.LocationDisplayName = incoming.Location.DisplayName;
        existing.CountryCode = incoming.Location.CountryCode;
        existing.Region = incoming.Location.Region;
        existing.UpdatedAtUtc = incoming.UpdatedAtUtc;
        ApplySeverity(existing, incoming);
        existing.Provider = incoming.Provider.ToString();
        existing.ProviderKey = incoming.ProviderKey;
    }

    private static void ApplySeverity(EventEntity existing, Event incoming)
    {
        existing.Severity = incoming.Severity.ToString();
        existing.SeverityRule = incoming.SeverityRule;
        existing.SeverityInputs = incoming.SeverityInputs;
    }

    private static bool SeverityMatches(EventEntity existing, Event incoming)
    {
        return existing.Severity == incoming.Severity.ToString()
            && existing.SeverityRule == incoming.SeverityRule
            && existing.SeverityInputs == incoming.SeverityInputs;
    }

    private static void ApplyProviderSourceUpdate(SourceItemEntity existing, SourceItem incoming)
    {
        existing.Title = incoming.Title;
        existing.Text = incoming.Text;
        existing.Url = incoming.Url?.AbsoluteUri;
        existing.AuthorDisplayName = incoming.AuthorDisplayName;
        existing.AuthorHandle = incoming.AuthorHandle;
        existing.PublishedAtUtc = incoming.PublishedAtUtc;
        existing.RetrievedAtUtc = incoming.RetrievedAtUtc;
        existing.Latitude = incoming.Location?.Latitude;
        existing.Longitude = incoming.Location?.Longitude;
        existing.LocationDisplayName = incoming.Location?.DisplayName;
        existing.CountryCode = incoming.Location?.CountryCode;
        existing.Region = incoming.Location?.Region;
    }

    private enum UpdateDecision
    {
        Create,
        Update,
        SeverityOnly,
        Unchanged,
        Conflict,
    }

    private static Event ToDomain(EventEntity entity)
    {
        return Event.Create(
            new EventId(entity.Id),
            entity.Title,
            entity.Summary,
            Enum.Parse<EventCategory>(entity.Category),
            Enum.Parse<EventStatus>(entity.Status),
            entity.StartedAtUtc,
            entity.LastUpdatedAtUtc,
            new Location(entity.Latitude, entity.Longitude, entity.LocationDisplayName, entity.CountryCode, entity.Region),
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            new EventSeverityAssessment(Enum.Parse<EventSeverity>(entity.Severity), entity.SeverityRule, entity.SeverityInputs),
            new EventProvenance(Enum.Parse<EventProvider>(entity.Provider), entity.ProviderKey));
    }

    private static SourceItem ToDomain(SourceItemEntity entity)
    {
        Location? location = null;
        if (entity.Latitude is not null && entity.Longitude is not null && !string.IsNullOrWhiteSpace(entity.LocationDisplayName))
        {
            location = new Location(entity.Latitude.Value, entity.Longitude.Value, entity.LocationDisplayName, entity.CountryCode, entity.Region);
        }

        return SourceItem.Create(
            new SourceItemId(entity.Id),
            Enum.Parse<SourceType>(entity.SourceType),
            entity.ExternalId,
            entity.Title,
            entity.Text,
            entity.Url is null ? null : new Uri(entity.Url),
            entity.AuthorDisplayName,
            entity.AuthorHandle,
            entity.PublishedAtUtc,
            entity.RetrievedAtUtc,
            location,
            entity.CreatedAtUtc);
    }

    private static EventEntity ToEntity(Event geopoliticalEvent)
    {
        return new EventEntity
        {
            Id = geopoliticalEvent.Id.Value,
            Title = geopoliticalEvent.Title,
            Summary = geopoliticalEvent.Summary,
            Category = geopoliticalEvent.Category.ToString(),
            Status = geopoliticalEvent.Status.ToString(),
            StartedAtUtc = geopoliticalEvent.StartedAtUtc,
            LastUpdatedAtUtc = geopoliticalEvent.LastUpdatedAtUtc,
            Latitude = geopoliticalEvent.Location.Latitude,
            Longitude = geopoliticalEvent.Location.Longitude,
            LocationDisplayName = geopoliticalEvent.Location.DisplayName,
            CountryCode = geopoliticalEvent.Location.CountryCode,
            Region = geopoliticalEvent.Location.Region,
            CreatedAtUtc = geopoliticalEvent.CreatedAtUtc,
            UpdatedAtUtc = geopoliticalEvent.UpdatedAtUtc,
            Severity = geopoliticalEvent.Severity.ToString(),
            SeverityRule = geopoliticalEvent.SeverityRule,
            SeverityInputs = geopoliticalEvent.SeverityInputs,
            Provider = geopoliticalEvent.Provider.ToString(),
            ProviderKey = geopoliticalEvent.ProviderKey,
        };
    }

    private static SourceItemEntity ToEntity(SourceItem sourceItem)
    {
        return new SourceItemEntity
        {
            Id = sourceItem.Id.Value,
            SourceType = sourceItem.SourceType.ToString(),
            ExternalId = sourceItem.ExternalId,
            Title = sourceItem.Title,
            Text = sourceItem.Text,
            Url = sourceItem.Url?.ToString(),
            AuthorDisplayName = sourceItem.AuthorDisplayName,
            AuthorHandle = sourceItem.AuthorHandle,
            PublishedAtUtc = sourceItem.PublishedAtUtc,
            RetrievedAtUtc = sourceItem.RetrievedAtUtc,
            Latitude = sourceItem.Location?.Latitude,
            Longitude = sourceItem.Location?.Longitude,
            LocationDisplayName = sourceItem.Location?.DisplayName,
            CountryCode = sourceItem.Location?.CountryCode,
            Region = sourceItem.Location?.Region,
            CreatedAtUtc = sourceItem.CreatedAtUtc,
        };
    }
}
