using System.Text.Json;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class EventSourceBridgeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidTypedRequestReachesApplicationUseCaseAndSerializesDisposition()
    {
        const string json = """{"type":"events.sources.attachXPost","payload":{"id":"evt-demo","url":"https://x.com/example/status/1759288472993702104"}}""";
        Assert.True(DesktopBridgeProtocol.TryParseRequest(json, out var request, out var parseError), parseError);
        var store = new RecordingStore(SourceAttachmentStoreDisposition.CreatedAndAttached);
        var handler = new EventSourceBridgeHandler(new EventCatalogService(store, new FixedTimeProvider(Now)));

        var responseJson = await handler.AttachXPostAsync(request!.EventId!, request.XPostUrl!);

        Assert.NotNull(store.AttachedSource);
        using var response = JsonDocument.Parse(responseJson);
        Assert.Equal(DesktopBridgeProtocol.EventSourcesAttachXPostResultMessageType, response.RootElement.GetProperty("type").GetString());
        Assert.Equal("CreatedAndAttached", response.RootElement.GetProperty("payload").GetProperty("disposition").GetString());
    }

    [Theory]
    [InlineData("https://x.com/home", "Enter a public X status URL.")]
    [InlineData("https://x.com/example/status/18446744073709551615", "The X status URL does not contain a valid post timestamp.")]
    public async Task InvalidXPostInputReturnsBoundedBridgeError(string url, string expectedMessage)
    {
        var handler = new EventSourceBridgeHandler(new EventCatalogService(
            new RecordingStore(SourceAttachmentStoreDisposition.CreatedAndAttached),
            new FixedTimeProvider(Now)));

        using var response = JsonDocument.Parse(await handler.AttachXPostAsync("evt-demo", url));

        Assert.Equal(DesktopBridgeProtocol.EventSourcesAttachXPostErrorMessageType, response.RootElement.GetProperty("type").GetString());
        Assert.Equal(expectedMessage, response.RootElement.GetProperty("payload").GetProperty("message").GetString());
    }

    [Theory]
    [InlineData(SourceAttachmentStoreDisposition.ExistingSourceAttached, "ExistingSourceAttached")]
    [InlineData(SourceAttachmentStoreDisposition.AlreadyAttached, "AlreadyAttached")]
    public async Task SerializesReuseDispositions(SourceAttachmentStoreDisposition storeDisposition, string expected)
    {
        var handler = new EventSourceBridgeHandler(new EventCatalogService(
            new RecordingStore(storeDisposition),
            new FixedTimeProvider(Now)));

        using var response = JsonDocument.Parse(await handler.AttachXPostAsync(
            "evt-demo",
            "https://x.com/example/status/1759288472993702104"));

        Assert.Equal(expected, response.RootElement.GetProperty("payload").GetProperty("disposition").GetString());
    }

    private sealed class RecordingStore(SourceAttachmentStoreDisposition disposition) : IEventStore
    {
        public SourceItem? AttachedSource { get; private set; }

        public Task<SourceAttachmentStoreResult> AttachSourceToEventAsync(EventId eventId, SourceItem sourceItem, CancellationToken cancellationToken = default)
        {
            AttachedSource = sourceItem;
            return Task.FromResult(new SourceAttachmentStoreResult(disposition, sourceItem));
        }

        public Task<IReadOnlyList<Event>> GetEventsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Event?> GetEventByIdAsync(EventId eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SourceItem>> GetSourcesForEventAsync(EventId eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<EventId, int>> GetSourceCountsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateEventAsync(Event geopoliticalEvent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateSourceItemAsync(SourceItem sourceItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AttachSourceToEventAsync(EventId eventId, SourceItemId sourceItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
