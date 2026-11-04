using System.Globalization;
using System.Text.Json;
using GeopoliticalMonitor.Application.Alerts;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.Firms;
using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Application.Ucdp;
using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Application.Refresh;
using GeopoliticalMonitor.Application.ReliefWeb;
using GeopoliticalMonitor.Application.Watchlists;
using GeopoliticalMonitor.Domain.Watchlists;
using GeopoliticalMonitor.Domain.Alerts;

namespace GeopoliticalMonitor.Application.DesktopBridge;

public static class DesktopBridgeProtocol
{
    public const string AppReadyMessageType = "app.ready";
    public const string AppInfoMessageType = "app.info";
    public const string PreferencesGetMessageType = "preferences.get";
    public const string PreferencesSetMessageType = "preferences.set";
    public const string PreferencesValueMessageType = "preferences.value";
    public const string PreferencesUpdatedMessageType = "preferences.updated";
    public const string PreferencesErrorMessageType = "preferences.error";
    public const string EventsListMessageType = "events.list";
    public const string EventsListResultMessageType = "events.list.result";
    public const string EventsGetMessageType = "events.get";
    public const string EventsGetResultMessageType = "events.get.result";
    public const string EventSourcesGetMessageType = "events.sources.get";
    public const string EventSourcesResultMessageType = "events.sources.result";
    public const string EventSourcesAttachXPostMessageType = "events.sources.attachXPost";
    public const string EventSourcesAttachXPostResultMessageType = "events.sources.attachXPost.result";
    public const string EventSourcesAttachXPostErrorMessageType = "events.sources.attachXPost.error";
    public const string ReliefWebSearchMessageType = "events.sources.reliefweb.search";
    public const string ReliefWebSearchResultMessageType = "events.sources.reliefweb.search.result";
    public const string ReliefWebSearchErrorMessageType = "events.sources.reliefweb.search.error";
    public const string ReliefWebAttachMessageType = "events.sources.reliefweb.attach";
    public const string ReliefWebAttachResultMessageType = "events.sources.reliefweb.attach.result";
    public const string ReliefWebAttachErrorMessageType = "events.sources.reliefweb.attach.error";
    public const string EventsErrorMessageType = "events.error";
    public const string GdacsRefreshMessageType = "providers.gdacs.refresh";
    public const string GdacsRefreshResultMessageType = "providers.gdacs.refresh.result";
    public const string GdacsRefreshErrorMessageType = "providers.gdacs.refresh.error";
    public const string UcdpRefreshMessageType = "providers.ucdp.refresh";
    public const string UcdpRefreshResultMessageType = "providers.ucdp.refresh.result";
    public const string UcdpRefreshErrorMessageType = "providers.ucdp.refresh.error";
    public const string ProvidersRefreshStatusMessageType = "providers.refresh.status";
    public const string ProvidersRefreshStatusGetMessageType = "providers.refresh.status.get";
    public const string FirmsRefreshMessageType = "providers.firms.refresh";
    public const string FirmsRefreshResultMessageType = "providers.firms.refresh.result";
    public const string FirmsRefreshErrorMessageType = "providers.firms.refresh.error";
    // FIRMS key status only: these requests carry no fields, and no response ever carries the key.
    public const string FirmsKeyStatusGetMessageType = "providers.firms.key.status.get";
    public const string FirmsKeySetMessageType = "providers.firms.key.set";
    public const string FirmsKeyRemoveMessageType = "providers.firms.key.remove";
    public const string FirmsKeyStatusMessageType = "providers.firms.key.status";
    public const string XPaneSideValueMessageType = "xPane.side.value";
    public const string WatchlistsListMessageType = "watchlists.list";
    public const string WatchlistsListResultMessageType = "watchlists.list.result";
    public const string WatchlistsCreateMessageType = "watchlists.create";
    public const string WatchlistsCreateResultMessageType = "watchlists.create.result";
    public const string WatchlistsRenameMessageType = "watchlists.rename";
    public const string WatchlistsRenameResultMessageType = "watchlists.rename.result";
    public const string WatchlistsDeleteMessageType = "watchlists.delete";
    public const string WatchlistsDeleteResultMessageType = "watchlists.delete.result";
    public const string WatchlistMembersGetMessageType = "watchlists.members.get";
    public const string WatchlistMembersGetResultMessageType = "watchlists.members.get.result";
    public const string WatchlistMembershipsGetForEventMessageType = "watchlists.memberships.getForEvent";
    public const string WatchlistMembershipsGetForEventResultMessageType = "watchlists.memberships.getForEvent.result";
    public const string WatchlistMembersSetMessageType = "watchlists.members.set";
    public const string WatchlistMembersSetResultMessageType = "watchlists.members.set.result";
    public const string WatchlistsErrorMessageType = "watchlists.error";
    public const string AlertsSummaryMessageType = "alerts.summary";
    public const string AlertsSummaryResultMessageType = "alerts.summary.result";
    public const string AlertRulesListMessageType = "alerts.rules.list";
    public const string AlertRulesListResultMessageType = "alerts.rules.list.result";
    public const string AlertRulesSetMessageType = "alerts.rules.set";
    public const string AlertRulesSetResultMessageType = "alerts.rules.set.result";
    public const string AlertsListMessageType = "alerts.list";
    public const string AlertsListResultMessageType = "alerts.list.result";
    public const string AlertsMarkReadMessageType = "alerts.markRead";
    public const string AlertsMarkReadResultMessageType = "alerts.markRead.result";
    public const string AlertsErrorMessageType = "alerts.error";

    public static bool TryParseRequest(string json, out DesktopBridgeRequest? request, out string? error)
    {
        request = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Message is empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "Message root must be an object.";
                return false;
            }

            JsonElement? payload = null;
            var hasType = false;
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!seenNames.Add(property.Name))
                {
                    error = $"Duplicate field '{property.Name}'.";
                    return false;
                }

                switch (property.Name)
                {
                    case "type":
                        hasType = true;
                        if (property.Value.ValueKind != JsonValueKind.String)
                        {
                            error = "Message type must be a string.";
                            return false;
                        }
                        break;
                    case "payload":
                        if (property.Value.ValueKind is not JsonValueKind.Object and not JsonValueKind.Null)
                        {
                            error = "Payload must be an object or null.";
                            return false;
                        }
                        payload = property.Value;
                        break;
                    default:
                        error = $"Unexpected field '{property.Name}'.";
                        return false;
                }
            }

            if (!hasType)
            {
                error = "Message type is required.";
                return false;
            }

            var type = document.RootElement.GetProperty("type").GetString();
            return type switch
            {
                AppReadyMessageType => TryCreateEmptyRequest(AppReadyMessageType, payload, "app.ready", out request, out error),
                EventsListMessageType => TryCreateEventsListRequest(payload, out request, out error),
                GdacsRefreshMessageType => TryCreateEmptyRequest(GdacsRefreshMessageType, payload, "providers.gdacs.refresh", out request, out error),
                UcdpRefreshMessageType => TryCreateEmptyRequest(UcdpRefreshMessageType, payload, "providers.ucdp.refresh", out request, out error),
                ProvidersRefreshStatusGetMessageType => TryCreateEmptyRequest(ProvidersRefreshStatusGetMessageType, payload, "providers.refresh.status.get", out request, out error),
                FirmsRefreshMessageType => TryCreateFirmsRefreshRequest(payload, out request, out error),
                FirmsKeyStatusGetMessageType => TryCreateEmptyRequest(FirmsKeyStatusGetMessageType, payload, "providers.firms.key.status.get", out request, out error),
                FirmsKeySetMessageType => TryCreateEmptyRequest(FirmsKeySetMessageType, payload, "providers.firms.key.set", out request, out error),
                FirmsKeyRemoveMessageType => TryCreateEmptyRequest(FirmsKeyRemoveMessageType, payload, "providers.firms.key.remove", out request, out error),
                WatchlistsListMessageType => TryCreateEmptyRequest(WatchlistsListMessageType, payload, "watchlists.list", out request, out error),
                AlertsSummaryMessageType => TryCreateEmptyRequest(AlertsSummaryMessageType, payload, "alerts.summary", out request, out error),
                AlertRulesListMessageType => TryCreateEmptyRequest(AlertRulesListMessageType, payload, "alerts.rules.list", out request, out error),
                AlertsListMessageType => TryCreateEmptyRequest(AlertsListMessageType, payload, "alerts.list", out request, out error),
                PreferencesGetMessageType => TryCreatePreferenceGetRequest(payload, out request, out error),
                PreferencesSetMessageType => TryCreatePreferenceSetRequest(payload, out request, out error),
                EventsGetMessageType => TryCreateEventRequest(EventsGetMessageType, payload, out request, out error),
                EventSourcesGetMessageType => TryCreateEventRequest(EventSourcesGetMessageType, payload, out request, out error),
                EventSourcesAttachXPostMessageType => TryCreateAttachXPostRequest(payload, out request, out error),
                ReliefWebSearchMessageType => TryCreateReliefWebSearchRequest(payload, out request, out error),
                ReliefWebAttachMessageType => TryCreateReliefWebAttachRequest(payload, out request, out error),
                WatchlistsCreateMessageType => TryCreateWatchlistRequest(WatchlistsCreateMessageType, payload, requireWatchlistId: false, requireName: true, requireEventId: false, requireMembership: false, out request, out error),
                WatchlistsRenameMessageType => TryCreateWatchlistRequest(WatchlistsRenameMessageType, payload, requireWatchlistId: true, requireName: true, requireEventId: false, requireMembership: false, out request, out error),
                WatchlistsDeleteMessageType => TryCreateWatchlistRequest(WatchlistsDeleteMessageType, payload, requireWatchlistId: true, requireName: false, requireEventId: false, requireMembership: false, out request, out error),
                WatchlistMembersGetMessageType => TryCreateWatchlistRequest(WatchlistMembersGetMessageType, payload, requireWatchlistId: true, requireName: false, requireEventId: false, requireMembership: false, out request, out error),
                WatchlistMembershipsGetForEventMessageType => TryCreateWatchlistRequest(WatchlistMembershipsGetForEventMessageType, payload, requireWatchlistId: false, requireName: false, requireEventId: true, requireMembership: false, out request, out error),
                WatchlistMembersSetMessageType => TryCreateWatchlistRequest(WatchlistMembersSetMessageType, payload, requireWatchlistId: true, requireName: false, requireEventId: true, requireMembership: true, out request, out error),
                AlertRulesSetMessageType => TryCreateAlertRuleSetRequest(payload, out request, out error),
                AlertsMarkReadMessageType => TryCreateAlertMarkReadRequest(payload, out request, out error),
                _ => RejectUnsupportedMessage(type, out error),
            };
        }
        catch (JsonException)
        {
            error = "Message is not valid JSON.";
            return false;
        }
    }

    public static string CreateAppInfoResponseJson(DesktopBridgeAppInfo appInfo)
    {
        var response = new
        {
            type = AppInfoMessageType,
            payload = new
            {
                appInfo.ApplicationName,
                appInfo.Shell,
                appInfo.UiHost,
                appInfo.Persistence,
                appInfo.ProductionInboundPortsRequired,
                XPaneSide = appInfo.XPaneSide.ToString(),
                appInfo.DemoEventsRemoved,
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreatePreferenceValueResponseJson(AccentTheme accentTheme)
    {
        var response = new
        {
            type = PreferencesValueMessageType,
            payload = new { key = AccentThemePreferenceService.PreferenceKey, value = accentTheme.ToString() },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreatePreferenceUpdatedResponseJson(AccentTheme accentTheme)
    {
        var response = new
        {
            type = PreferencesUpdatedMessageType,
            payload = new { key = AccentThemePreferenceService.PreferenceKey, value = accentTheme.ToString() },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreatePreferenceErrorResponseJson(string message)
    {
        var response = new
        {
            type = PreferencesErrorMessageType,
            payload = new { key = AccentThemePreferenceService.PreferenceKey, message },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateXPaneSideValueResponseJson(XPaneSide side)
    {
        var response = new
        {
            type = XPaneSideValueMessageType,
            payload = new { value = side.ToString() },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateProvidersRefreshStatusJson(IReadOnlyList<ProviderRefreshStatus> statuses)
    {
        var response = new
        {
            type = ProvidersRefreshStatusMessageType,
            payload = new
            {
                providers = statuses.Select(CreateProviderStatusPayload).ToArray(),
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    private static Dictionary<string, object?> CreateProviderStatusPayload(ProviderRefreshStatus status)
    {
        var payload = new Dictionary<string, object?>
        {
            ["provider"] = status.Provider.ToString(),
            ["displayName"] = status.DisplayName,
            ["state"] = status.State.ToString(),
            ["lastSuccessAtUtc"] = status.LastSuccessAtUtc,
            ["nextRefreshAtUtc"] = status.NextRefreshAtUtc,
            ["message"] = ProviderStatusMessages.ToText(status.Message),
            ["newAlerts"] = status.NewAlerts,
        };

        // Optional fields appear only for providers that publish them (UCDP), so other payloads stay unchanged.
        if (status.CadenceLabel is not null)
        {
            payload["cadenceLabel"] = status.CadenceLabel;
        }

        if (status.DataThrough is not null)
        {
            payload["dataThrough"] = status.DataThrough;
        }

        return payload;
    }

    public static string CreateEventsListResultResponseJson(IReadOnlyList<EventDto> events)
        => CreateEventsListResultResponseJson(new EventWindowResult(events, events.Count, false), requestId: null);

    public static string CreateEventsListResultResponseJson(EventWindowResult result, int? requestId)
    {
        var response = new
        {
            type = EventsListResultMessageType,
            payload = new
            {
                events = result.Events,
                totalCount = result.TotalCount,
                capped = result.Capped,
                requestId,
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateEventGetResultResponseJson(EventDto? geopoliticalEvent)
    {
        var response = new { type = EventsGetResultMessageType, payload = new { eventItem = geopoliticalEvent } };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateEventSourcesResultResponseJson(string eventId, IReadOnlyList<SourceItemDto> sources)
    {
        var response = new { type = EventSourcesResultMessageType, payload = new { eventId, sources } };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateEventSourcesAttachXPostResultResponseJson(string eventId, AttachXPostSourceResult result)
    {
        var response = new
        {
            type = EventSourcesAttachXPostResultMessageType,
            payload = new
            {
                eventId,
                disposition = result.Disposition.ToString(),
                source = result.Source,
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateEventSourcesAttachXPostErrorResponseJson(string eventId, string message)
    {
        var response = new
        {
            type = EventSourcesAttachXPostErrorMessageType,
            payload = new { eventId, message },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateEventsErrorResponseJson(string message)
    {
        var response = new { type = EventsErrorMessageType, payload = new { message } };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateReliefWebSearchResultResponseJson(string eventId, ReliefWebSearchResult result)
    {
        var response = new
        {
            type = ReliefWebSearchResultMessageType,
            payload = new
            {
                eventId,
                candidates = result.Candidates ?? [],
                result.SkippedMalformedReports,
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateReliefWebSearchErrorResponseJson(
        string eventId,
        ReliefWebSearchStatus status,
        string message)
    {
        var response = new
        {
            type = ReliefWebSearchErrorMessageType,
            payload = new { eventId, status = status.ToString(), message },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateReliefWebAttachResultResponseJson(
        string eventId,
        string reportId,
        ReliefWebAttachResult result)
    {
        var response = new
        {
            type = ReliefWebAttachResultMessageType,
            payload = new
            {
                eventId,
                reportId,
                disposition = result.Status.ToString(),
                source = result.Source,
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateReliefWebAttachErrorResponseJson(
        string eventId,
        string reportId,
        ReliefWebAttachStatus status,
        string message)
    {
        var response = new
        {
            type = ReliefWebAttachErrorMessageType,
            payload = new { eventId, reportId, status = status.ToString(), message },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateGdacsRefreshResultResponseJson(GdacsRefreshResult result)
    {
        var response = new
        {
            type = GdacsRefreshResultMessageType,
            payload = new
            {
                status = result.Status.ToString(),
                result.NewEvents,
                result.UpdatedEvents,
                result.NewSources,
                result.UpdatedSources,
                result.UnchangedRecords,
                result.SkippedMalformedRecords,
                result.Conflicts,
                result.NewAlerts,
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateGdacsRefreshErrorResponseJson(GdacsRefreshStatus status, string message)
    {
        var response = new
        {
            type = GdacsRefreshErrorMessageType,
            payload = new { status = status.ToString(), message },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateUcdpRefreshResultResponseJson(UcdpRefreshResult result)
    {
        var response = new
        {
            type = UcdpRefreshResultMessageType,
            payload = new
            {
                status = result.Status.ToString(),
                newestRelease = result.NewestRelease is { } ordinal && UcdpRelease.TryFromOrdinal(ordinal, out var release) ? release.MonthKey : null,
                result.ReleasesImported,
                result.NewEvents,
                result.UpdatedEvents,
                result.UnchangedRecords,
                result.Conflicts,
                result.SkippedMalformedRecords,
                result.SkippedInvalidCoordinates,
                result.SkippedImpreciseLocation,
                result.SkippedZeroFatalities,
                result.SkippedOutsideWindow,
                result.DroppedByCap,
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateUcdpRefreshErrorResponseJson(UcdpRefreshStatus status, string message)
    {
        var response = new
        {
            type = UcdpRefreshErrorMessageType,
            payload = new { status = status.ToString(), message },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateFirmsRefreshResultResponseJson(FirmsSnapshot snapshot)
    {
        var response = new
        {
            type = FirmsRefreshResultMessageType,
            payload = new
            {
                observations = snapshot.Observations.Select(item => new
                {
                    item.Id,
                    item.Latitude,
                    item.Longitude,
                    item.AcquiredAtUtc,
                    item.Source,
                    item.Satellite,
                    item.Confidence,
                    item.Frp,
                    item.BrightnessTemperatureI4,
                    item.BrightnessTemperatureI5,
                    item.Scan,
                    item.Track,
                    item.ProcessingVersion,
                    item.DayNight,
                }),
                observationCount = snapshot.Observations.Count,
                snapshot.RefreshedAtUtc,
                snapshot.OldestAcquiredAtUtc,
                snapshot.NewestAcquiredAtUtc,
                coverage = new
                {
                    snapshot.Coverage.West,
                    snapshot.Coverage.South,
                    snapshot.Coverage.East,
                    snapshot.Coverage.North,
                },
                snapshot.SkippedMalformedRecords,
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateFirmsRefreshErrorResponseJson(FirmsRefreshStatus status, string message)
    {
        var response = new
        {
            type = FirmsRefreshErrorMessageType,
            payload = new { status = status.ToString(), message },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    /// <summary>Key status only: configured, source, format warning and store availability. Never the key.</summary>
    public static string CreateFirmsKeyStatusResponseJson(FirmsKeyStatus status, FirmsKeyChangeOutcome? outcome = null)
    {
        var response = new
        {
            type = FirmsKeyStatusMessageType,
            payload = new
            {
                configured = status.Configured,
                source = status.Source.ToString(),
                formatWarning = status.FormatWarning,
                storeUnavailable = status.StoreUnavailable,
                outcome = outcome?.ToString(),
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateWatchlistsListResultResponseJson(IReadOnlyList<WatchlistSummaryDto> watchlists)
    {
        var response = new { type = WatchlistsListResultMessageType, payload = new { watchlists } };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateWatchlistCreateResultResponseJson(WatchlistSummaryDto watchlist)
    {
        var response = new { type = WatchlistsCreateResultMessageType, payload = new { watchlist } };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateWatchlistRenameResultResponseJson(RenameWatchlistResult result)
    {
        var response = new
        {
            type = WatchlistsRenameResultMessageType,
            payload = new { disposition = result.Disposition.ToString(), watchlist = result.Watchlist },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateWatchlistDeleteResultResponseJson(DeleteWatchlistResult result)
    {
        var response = new
        {
            type = WatchlistsDeleteResultMessageType,
            payload = new { watchlistId = result.WatchlistId, disposition = result.Disposition.ToString() },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateWatchlistMembersResultResponseJson(WatchlistEventIdsResult result)
    {
        var response = new
        {
            type = WatchlistMembersGetResultMessageType,
            payload = new { watchlistId = result.WatchlistId, eventIds = result.EventIds },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateEventWatchlistMembershipsResultResponseJson(EventWatchlistIdsResult result)
    {
        var response = new
        {
            type = WatchlistMembershipsGetForEventResultMessageType,
            payload = new { eventId = result.EventId, watchlistIds = result.WatchlistIds },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateWatchlistMembershipSetResultResponseJson(SetWatchlistMembershipResult result)
    {
        var response = new
        {
            type = WatchlistMembersSetResultMessageType,
            payload = new
            {
                watchlistId = result.WatchlistId,
                eventId = result.EventId,
                result.IsMember,
                disposition = result.Disposition.ToString(),
                result.EventCount,
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateWatchlistErrorResponseJson(
        string operation,
        string code,
        string message,
        string? watchlistId = null,
        string? eventId = null)
    {
        var response = new
        {
            type = WatchlistsErrorMessageType,
            payload = new { operation, code, message, watchlistId, eventId },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateAlertSummaryResultResponseJson(long unreadCount)
    {
        var response = new { type = AlertsSummaryResultMessageType, payload = new { unreadCount } };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateAlertRulesListResultResponseJson(IReadOnlyList<AlertRuleDto> rules)
    {
        var response = new { type = AlertRulesListResultMessageType, payload = new { rules } };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateAlertRuleSetResultResponseJson(SetAlertRuleResult result)
    {
        var response = new
        {
            type = AlertRulesSetResultMessageType,
            payload = new { disposition = result.Disposition.ToString(), rule = result.Rule },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateAlertsListResultResponseJson(AlertListResult result)
    {
        var response = new
        {
            type = AlertsListResultMessageType,
            payload = new { alerts = result.Alerts, result.UnreadCount, result.HasMore },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateAlertMarkReadResultResponseJson(MarkAlertReadResult result)
    {
        var response = new
        {
            type = AlertsMarkReadResultMessageType,
            payload = new
            {
                alertId = result.AlertId,
                readAtUtc = result.ReadAtUtc,
                disposition = result.Disposition.ToString(),
            },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    public static string CreateAlertErrorResponseJson(
        string operation,
        string code,
        string message,
        string? watchlistId = null,
        string? alertId = null)
    {
        var response = new
        {
            type = AlertsErrorMessageType,
            payload = new { operation, code, message, watchlistId, alertId },
        };
        return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
    }

    private static bool TryCreateEmptyRequest(string type, JsonElement? payload, string label, out DesktopBridgeRequest? request, out string? error)
    {
        request = null;
        error = null;

        if (payload is { ValueKind: JsonValueKind.Object } payloadObject && payloadObject.EnumerateObject().Any())
        {
            error = $"{label} does not accept payload fields.";
            return false;
        }

        request = new DesktopBridgeRequest(type);
        return true;
    }

    /// <summary>
    /// events.list with optional typed criteria. No fields (or a null payload) keeps the old meaning: every criterion
    /// at its default, with the window cap applied. Unknown, duplicate, null or mistyped fields are rejected.
    /// </summary>
    private static bool TryCreateEventsListRequest(JsonElement? payload, out DesktopBridgeRequest? request, out string? error)
    {
        request = null;
        error = null;
        var window = new EventWindowRequest();
        int? requestId = null;

        if (payload is { ValueKind: JsonValueKind.Object } payloadObject)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in payloadObject.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    error = $"Duplicate events.list field '{property.Name}'.";
                    return false;
                }

                switch (property.Name)
                {
                    case "requestId":
                        if (property.Value.ValueKind != JsonValueKind.Number
                            || !property.Value.TryGetInt32(out var parsedRequestId)
                            || parsedRequestId < 0)
                        {
                            error = "events.list requestId must be a non-negative 32-bit integer.";
                            return false;
                        }

                        requestId = parsedRequestId;
                        break;
                    case "range":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !EventWindowRules.TryParseRange(property.Value.GetString(), out var range))
                        {
                            error = "events.list range is not supported.";
                            return false;
                        }

                        window = window with { Range = range };
                        break;
                    case "search":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || property.Value.GetString() is not { } search
                            || search.Length > EventWindowRules.MaximumSearchLength)
                        {
                            error = $"events.list search must be a string of at most {EventWindowRules.MaximumSearchLength} characters.";
                            return false;
                        }

                        window = window with { Search = search };
                        break;
                    case "minimumSeverity":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !EventWindowRules.TryParseMinimumSeverity(property.Value.GetString(), out var minimumSeverity))
                        {
                            error = "events.list minimumSeverity is not supported.";
                            return false;
                        }

                        window = window with { MinimumSeverity = minimumSeverity };
                        break;
                    case "watchlistId":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !WatchlistId.TryParse(property.Value.GetString(), out _))
                        {
                            error = "events.list watchlistId is invalid.";
                            return false;
                        }

                        window = window with { WatchlistId = property.Value.GetString() };
                        break;
                    case "keepEventId":
                        if (property.Value.ValueKind != JsonValueKind.String
                            || !EventCatalogService.TryParseEventId(property.Value.GetString(), out _))
                        {
                            error = "events.list keepEventId is invalid.";
                            return false;
                        }

                        window = window with { KeepEventId = property.Value.GetString() };
                        break;
                    default:
                        error = $"Unexpected events.list field '{property.Name}'.";
                        return false;
                }
            }
        }

        request = new DesktopBridgeRequest(EventsListMessageType, EventWindow: window, EventsListRequestId: requestId);
        return true;
    }

    private static bool TryCreateEventRequest(string type, JsonElement? payload, out DesktopBridgeRequest? request, out string? error)
    {
        request = null;
        error = null;

        if (payload is not { ValueKind: JsonValueKind.Object } payloadObject)
        {
            error = "Event payload must be an object.";
            return false;
        }

        string? eventId = null;
        foreach (var property in payloadObject.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        error = "Event id must be a string.";
                        return false;
                    }
                    eventId = property.Value.GetString();
                    break;
                default:
                    error = $"Unexpected event payload field '{property.Name}'.";
                    return false;
            }
        }

        if (!EventCatalogService.TryParseEventId(eventId, out _))
        {
            error = "Event id is invalid.";
            return false;
        }

        request = new DesktopBridgeRequest(type, EventId: eventId);
        return true;
    }

    private static bool TryCreateFirmsRefreshRequest(
        JsonElement? payload,
        out DesktopBridgeRequest? request,
        out string? error)
    {
        request = null;
        error = null;
        if (payload is not { ValueKind: JsonValueKind.Object } payloadObject)
        {
            error = "NASA FIRMS refresh payload must be an object.";
            return false;
        }

        double? west = null;
        double? south = null;
        double? east = null;
        double? north = null;
        var seenFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in payloadObject.EnumerateObject())
        {
            if (!seenFields.Add(property.Name))
            {
                error = $"Duplicate NASA FIRMS field '{property.Name}'.";
                return false;
            }

            if (property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetDouble(out var value)
                || !double.IsFinite(value))
            {
                error = $"NASA FIRMS field '{property.Name}' must be a finite number.";
                return false;
            }

            switch (property.Name)
            {
                case "west": west = value; break;
                case "south": south = value; break;
                case "east": east = value; break;
                case "north": north = value; break;
                default:
                    error = $"Unexpected NASA FIRMS field '{property.Name}'.";
                    return false;
            }
        }

        if (west is null || south is null || east is null || north is null)
        {
            error = "NASA FIRMS viewport requires west, south, east, and north.";
            return false;
        }

        request = new DesktopBridgeRequest(
            FirmsRefreshMessageType,
            FirmsWest: west,
            FirmsSouth: south,
            FirmsEast: east,
            FirmsNorth: north);
        return true;
    }

    private static bool TryCreateWatchlistRequest(
        string type,
        JsonElement? payload,
        bool requireWatchlistId,
        bool requireName,
        bool requireEventId,
        bool requireMembership,
        out DesktopBridgeRequest? request,
        out string? error)
    {
        request = null;
        error = null;
        if (payload is not { ValueKind: JsonValueKind.Object } payloadObject)
        {
            error = "Watchlist payload must be an object.";
            return false;
        }

        string? watchlistId = null;
        string? name = null;
        string? eventId = null;
        bool? isMember = null;
        foreach (var property in payloadObject.EnumerateObject())
        {
            switch (property.Name)
            {
                case "watchlistId" when requireWatchlistId:
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        error = "Watchlist id must be a string.";
                        return false;
                    }
                    watchlistId = property.Value.GetString();
                    break;
                case "name" when requireName:
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        error = "Watchlist name must be a string.";
                        return false;
                    }
                    name = property.Value.GetString();
                    break;
                case "eventId" when requireEventId:
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        error = "Event id must be a string.";
                        return false;
                    }
                    eventId = property.Value.GetString();
                    break;
                case "isMember" when requireMembership:
                    if (property.Value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                    {
                        error = "Watchlist membership state must be a boolean.";
                        return false;
                    }
                    isMember = property.Value.GetBoolean();
                    break;
                default:
                    error = $"Unexpected watchlist payload field '{property.Name}'.";
                    return false;
            }
        }

        if (requireWatchlistId && !WatchlistId.TryParse(watchlistId, out _))
        {
            error = "Watchlist id is invalid.";
            return false;
        }

        if (requireName)
        {
            if (name is null || name.Length > Watchlist.MaximumNameLength)
            {
                error = "Watchlist name is invalid.";
                return false;
            }

            try
            {
                _ = Watchlist.NormalizeName(name);
            }
            catch (ArgumentException)
            {
                error = "Watchlist name is invalid.";
                return false;
            }
        }

        if (requireEventId && !EventCatalogService.TryParseEventId(eventId, out _))
        {
            error = "Event id is invalid.";
            return false;
        }

        if (requireMembership && isMember is null)
        {
            error = "Watchlist membership state is required.";
            return false;
        }

        request = new DesktopBridgeRequest(
            type,
            EventId: eventId,
            WatchlistId: watchlistId,
            WatchlistName: name,
            WatchlistIsMember: isMember);
        return true;
    }

    private static bool TryCreateAlertRuleSetRequest(
        JsonElement? payload,
        out DesktopBridgeRequest? request,
        out string? error)
    {
        request = null;
        error = null;
        if (payload is not { ValueKind: JsonValueKind.Object } payloadObject)
        {
            error = "Alert rule payload must be an object.";
            return false;
        }

        string? watchlistId = null;
        bool? enabled = null;
        foreach (var property in payloadObject.EnumerateObject())
        {
            switch (property.Name)
            {
                case "watchlistId":
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        error = "Watchlist id must be a string.";
                        return false;
                    }
                    watchlistId = property.Value.GetString();
                    break;
                case "enabled":
                    if (property.Value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                    {
                        error = "Alert rule enabled state must be a boolean.";
                        return false;
                    }
                    enabled = property.Value.GetBoolean();
                    break;
                default:
                    error = $"Unexpected Alert rule payload field '{property.Name}'.";
                    return false;
            }
        }

        if (!WatchlistId.TryParse(watchlistId, out _))
        {
            error = "Watchlist id is invalid.";
            return false;
        }

        if (enabled is null)
        {
            error = "Alert rule enabled state is required.";
            return false;
        }

        request = new DesktopBridgeRequest(
            AlertRulesSetMessageType,
            WatchlistId: watchlistId,
            AlertRuleEnabled: enabled);
        return true;
    }

    private static bool TryCreateAlertMarkReadRequest(
        JsonElement? payload,
        out DesktopBridgeRequest? request,
        out string? error)
    {
        request = null;
        error = null;
        if (payload is not { ValueKind: JsonValueKind.Object } payloadObject)
        {
            error = "Alert payload must be an object.";
            return false;
        }

        string? alertId = null;
        foreach (var property in payloadObject.EnumerateObject())
        {
            if (property.Name != "alertId")
            {
                error = $"Unexpected Alert payload field '{property.Name}'.";
                return false;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                error = "Alert id must be a string.";
                return false;
            }

            alertId = property.Value.GetString();
        }

        if (!AlertId.TryParse(alertId, out _))
        {
            error = "Alert id is invalid.";
            return false;
        }

        request = new DesktopBridgeRequest(AlertsMarkReadMessageType, AlertId: alertId);
        return true;
    }

    private static bool TryCreateAttachXPostRequest(JsonElement? payload, out DesktopBridgeRequest? request, out string? error)
    {
        request = null;
        error = null;
        if (payload is not { ValueKind: JsonValueKind.Object } payloadObject)
        {
            error = "X post attachment payload must be an object.";
            return false;
        }

        string? eventId = null;
        string? url = null;
        foreach (var property in payloadObject.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        error = "Event id must be a string.";
                        return false;
                    }
                    eventId = property.Value.GetString();
                    break;
                case "url":
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        error = "X post URL must be a string.";
                        return false;
                    }
                    url = property.Value.GetString();
                    break;
                default:
                    error = $"Unexpected X post attachment field '{property.Name}'.";
                    return false;
            }
        }

        if (!EventCatalogService.TryParseEventId(eventId, out _))
        {
            error = "Event id is invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048)
        {
            error = "X post URL is invalid.";
            return false;
        }

        request = new DesktopBridgeRequest(EventSourcesAttachXPostMessageType, EventId: eventId, XPostUrl: url);
        return true;
    }

    private static bool TryCreateReliefWebSearchRequest(JsonElement? payload, out DesktopBridgeRequest? request, out string? error)
    {
        request = null;
        error = null;
        if (payload is not { ValueKind: JsonValueKind.Object } payloadObject)
        {
            error = "ReliefWeb search payload must be an object.";
            return false;
        }

        string? eventId = null;
        string? queryText = null;
        string? fromDateUtc = null;
        string? toDateUtc = null;
        string? countryIso3 = null;
        foreach (var property in payloadObject.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                error = $"ReliefWeb search field '{property.Name}' must be a string.";
                return false;
            }

            switch (property.Name)
            {
                case "eventId": eventId = property.Value.GetString(); break;
                case "queryText": queryText = property.Value.GetString(); break;
                case "fromDateUtc": fromDateUtc = property.Value.GetString(); break;
                case "toDateUtc": toDateUtc = property.Value.GetString(); break;
                case "countryIso3": countryIso3 = property.Value.GetString(); break;
                default:
                    error = $"Unexpected ReliefWeb search field '{property.Name}'.";
                    return false;
            }
        }

        if (!EventCatalogService.TryParseEventId(eventId, out _))
        {
            error = "Event id is invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(queryText) || queryText.Trim().Length > ReliefWebReportDiscoveryService.MaximumQueryLength)
        {
            error = "ReliefWeb query is invalid.";
            return false;
        }

        if (!DateTimeOffset.TryParse(fromDateUtc, out _) || !DateTimeOffset.TryParse(toDateUtc, out _))
        {
            error = "ReliefWeb search dates are invalid.";
            return false;
        }

        if (countryIso3 is not null
            && (countryIso3.Length != 3 || !countryIso3.All(char.IsAsciiLetter)))
        {
            error = "ReliefWeb country ISO3 is invalid.";
            return false;
        }

        request = new DesktopBridgeRequest(
            ReliefWebSearchMessageType,
            EventId: eventId,
            ReliefWebQueryText: queryText,
            ReliefWebFromDateUtc: fromDateUtc,
            ReliefWebToDateUtc: toDateUtc,
            ReliefWebCountryIso3: countryIso3);
        return true;
    }

    private static bool TryCreateReliefWebAttachRequest(JsonElement? payload, out DesktopBridgeRequest? request, out string? error)
    {
        request = null;
        error = null;
        if (payload is not { ValueKind: JsonValueKind.Object } payloadObject)
        {
            error = "ReliefWeb attachment payload must be an object.";
            return false;
        }

        string? eventId = null;
        string? reportId = null;
        foreach (var property in payloadObject.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                error = $"ReliefWeb attachment field '{property.Name}' must be a string.";
                return false;
            }

            switch (property.Name)
            {
                case "eventId": eventId = property.Value.GetString(); break;
                case "reportId": reportId = property.Value.GetString(); break;
                default:
                    error = $"Unexpected ReliefWeb attachment field '{property.Name}'.";
                    return false;
            }
        }

        if (!EventCatalogService.TryParseEventId(eventId, out _))
        {
            error = "Event id is invalid.";
            return false;
        }

        if (!long.TryParse(reportId, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedReportId) || parsedReportId <= 0)
        {
            error = "ReliefWeb report id is invalid.";
            return false;
        }

        request = new DesktopBridgeRequest(ReliefWebAttachMessageType, EventId: eventId, ReliefWebReportId: reportId);
        return true;
    }

    private static bool TryCreatePreferenceGetRequest(JsonElement? payload, out DesktopBridgeRequest? request, out string? error)
    {
        request = null;
        if (!TryReadPreferencePayload(payload, requireValue: false, out var key, out _, out error))
        {
            return false;
        }

        request = new DesktopBridgeRequest(PreferencesGetMessageType, key);
        return true;
    }

    private static bool TryCreatePreferenceSetRequest(JsonElement? payload, out DesktopBridgeRequest? request, out string? error)
    {
        request = null;
        if (!TryReadPreferencePayload(payload, requireValue: true, out var key, out var value, out error))
        {
            return false;
        }

        if (!AccentThemePreferenceService.TryParseAccentTheme(value, out _))
        {
            error = "Unsupported AccentTheme value.";
            return false;
        }

        request = new DesktopBridgeRequest(PreferencesSetMessageType, key, value);
        return true;
    }

    private static bool TryReadPreferencePayload(JsonElement? payload, bool requireValue, out string? key, out string? value, out string? error)
    {
        key = null;
        value = null;
        error = null;

        if (payload is not { ValueKind: JsonValueKind.Object } payloadObject)
        {
            error = "Preference payload must be an object.";
            return false;
        }

        foreach (var property in payloadObject.EnumerateObject())
        {
            switch (property.Name)
            {
                case "key":
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        error = "Preference key must be a string.";
                        return false;
                    }
                    key = property.Value.GetString();
                    break;
                case "value":
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        error = "Preference value must be a string.";
                        return false;
                    }
                    value = property.Value.GetString();
                    break;
                default:
                    error = $"Unexpected preference payload field '{property.Name}'.";
                    return false;
            }
        }

        if (!AccentThemePreferenceService.TryParsePreferenceKey(key))
        {
            error = "Unsupported preference key.";
            return false;
        }

        if (requireValue && value is null)
        {
            error = "Preference value is required.";
            return false;
        }

        if (!requireValue && value is not null)
        {
            error = "Preference get does not accept a value.";
            return false;
        }

        return true;
    }

    private static bool RejectUnsupportedMessage(string? type, out string? error)
    {
        error = $"Unsupported message type '{type}'.";
        return false;
    }
}
