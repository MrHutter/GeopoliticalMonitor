using System.Text.Json;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.ReliefWeb;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class ReliefWebBridgeHandlerTests
{
    [Fact]
    public async Task ReturnsTypedConfigurationUnavailableSearchError()
    {
        var handler = CreateHandler(new FailingClient(ReliefWebFailureKind.ConfigurationUnavailable));
        var response = await handler.SearchAsync(new DesktopBridgeRequest(
            DesktopBridgeProtocol.ReliefWebSearchMessageType,
            EventId: "evt-demo",
            ReliefWebQueryText: "flood",
            ReliefWebFromDateUtc: "2026-09-01T00:00:00Z",
            ReliefWebToDateUtc: "2026-09-30T00:00:00Z"));

        using var json = JsonDocument.Parse(response);
        Assert.Equal(DesktopBridgeProtocol.ReliefWebSearchErrorMessageType, json.RootElement.GetProperty("type").GetString());
        Assert.Equal("ConfigurationUnavailable", json.RootElement.GetProperty("payload").GetProperty("status").GetString());
        Assert.Equal("ReliefWeb integration is not configured.", json.RootElement.GetProperty("payload").GetProperty("message").GetString());
    }

    [Fact]
    public async Task ReturnsTypedAttachDisposition()
    {
        var handler = CreateHandler(new SuccessfulClient());
        var response = await handler.AttachAsync(new DesktopBridgeRequest(
            DesktopBridgeProtocol.ReliefWebAttachMessageType,
            EventId: "evt-demo",
            ReliefWebReportId: "5187658"));

        using var json = JsonDocument.Parse(response);
        Assert.Equal(DesktopBridgeProtocol.ReliefWebAttachResultMessageType, json.RootElement.GetProperty("type").GetString());
        Assert.Equal("CreatedAndAttached", json.RootElement.GetProperty("payload").GetProperty("disposition").GetString());
        Assert.Equal("src-reliefweb-report-5187658", json.RootElement.GetProperty("payload").GetProperty("source").GetProperty("id").GetString());
    }

    private static ReliefWebBridgeHandler CreateHandler(IReliefWebReportClient client)
    {
        var store = new FakeStore();
        return new ReliefWebBridgeHandler(new ReliefWebReportDiscoveryService(client, store, store));
    }

    private sealed class FailingClient(ReliefWebFailureKind kind) : IReliefWebReportClient
    {
        public Task<ReliefWebReportBatch> SearchReportsAsync(ReliefWebSearchCriteria criteria, CancellationToken cancellationToken = default)
            => throw new ReliefWebClientException(kind, "failure");
        public Task<ReliefWebReportRecord> GetReportAsync(long reportId, CancellationToken cancellationToken = default)
            => throw new ReliefWebClientException(kind, "failure");
    }

    private sealed class SuccessfulClient : IReliefWebReportClient
    {
        private static readonly ReliefWebReportRecord Report = new(
            5187658, "Flood report", new Uri("https://reliefweb.int/report/example/flood"), null,
            DateTimeOffset.Parse("2026-09-20T00:00:00Z"), null, null, [], [], [], [], []);
        public Task<ReliefWebReportBatch> SearchReportsAsync(ReliefWebSearchCriteria criteria, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReliefWebReportBatch([Report], 0));
        public Task<ReliefWebReportRecord> GetReportAsync(long reportId, CancellationToken cancellationToken = default)
            => Task.FromResult(Report);
    }

    private sealed class FakeStore : IEventStore, IReliefWebSourceStore
    {
        private static readonly Event ExistingEvent = Event.Create(
            new EventId("evt-demo"), "Flood", "Summary", EventCategory.Humanitarian, EventStatus.Active,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new Location(1, 2, "Example"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            new EventSeverityAssessment(EventSeverity.Low, EventSeverityAssessment.ManualRule, "test"), new EventProvenance(EventProvider.Manual));

        public Task<Event?> GetEventByIdAsync(EventId eventId, CancellationToken cancellationToken = default) => Task.FromResult<Event?>(ExistingEvent);
        public Task<ReliefWebAttachmentStoreResult> AttachReliefWebSourceAsync(EventId eventId, SourceItem sourceItem, CancellationToken cancellationToken = default)
            => Task.FromResult(new ReliefWebAttachmentStoreResult(ReliefWebAttachmentStoreDisposition.CreatedAndAttached, sourceItem));
        public Task<IReadOnlyList<Event>> GetEventsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SourceItem>> GetSourcesForEventAsync(EventId eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<EventId, int>> GetSourceCountsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateEventAsync(Event geopoliticalEvent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateSourceItemAsync(SourceItem sourceItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AttachSourceToEventAsync(EventId eventId, SourceItemId sourceItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SourceAttachmentStoreResult> AttachSourceToEventAsync(EventId eventId, SourceItem sourceItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
