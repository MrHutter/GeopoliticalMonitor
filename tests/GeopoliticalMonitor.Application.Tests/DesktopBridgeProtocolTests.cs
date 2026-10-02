using System.Text.Json;
using GeopoliticalMonitor.Application;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.Preferences;

namespace GeopoliticalMonitor.Application.Tests;

public class DesktopBridgeProtocolTests
{
    [Fact]
    public void AcceptsNarrowReliefWebSearchAndAttachRequests()
    {
        Assert.True(DesktopBridgeProtocol.TryParseRequest(
            """{"type":"events.sources.reliefweb.search","payload":{"eventId":"evt-demo","queryText":"flood","fromDateUtc":"2026-09-01T00:00:00Z","toDateUtc":"2026-09-20T00:00:00Z","countryIso3":"EXA"}}""",
            out var search,
            out var searchError), searchError);
        Assert.Equal("flood", search?.ReliefWebQueryText);
        Assert.Equal("EXA", search?.ReliefWebCountryIso3);

        Assert.True(DesktopBridgeProtocol.TryParseRequest(
            """{"type":"events.sources.reliefweb.attach","payload":{"eventId":"evt-demo","reportId":"5187658"}}""",
            out var attach,
            out var attachError), attachError);
        Assert.Equal("5187658", attach?.ReliefWebReportId);
    }

    [Theory]
    [InlineData("""{"type":"events.sources.reliefweb.search","payload":{"eventId":"evt-demo","queryText":"flood","fromDateUtc":"bad","toDateUtc":"2026-09-20T00:00:00Z"}}""")]
    [InlineData("""{"type":"events.sources.reliefweb.search","payload":{"eventId":"evt-demo","queryText":"flood","fromDateUtc":"2026-09-01T00:00:00Z","toDateUtc":"2026-09-20T00:00:00Z","endpoint":"https://example.com"}}""")]
    [InlineData("""{"type":"events.sources.reliefweb.search","payload":{"eventId":"bad id","queryText":"flood","fromDateUtc":"2026-09-01T00:00:00Z","toDateUtc":"2026-09-20T00:00:00Z"}}""")]
    [InlineData("""{"type":"events.sources.reliefweb.search","payload":{"eventId":"evt-demo","queryText":"flood","fromDateUtc":"2026-09-01T00:00:00Z","toDateUtc":"2026-09-20T00:00:00Z","countryIso3":"XX"}}""")]
    [InlineData("""{"type":"events.sources.reliefweb.attach","payload":{"eventId":"evt-demo","reportId":"not-a-number"}}""")]
    [InlineData("""{"type":"events.sources.reliefweb.attach","payload":{"eventId":"evt-demo","reportId":" 1"}}""")]
    [InlineData("""{"type":"events.sources.reliefweb.attach","payload":{"eventId":"evt-demo","reportId":"1","url":"https://example.com"}}""")]
    public void RejectsMalformedOrBroadenedReliefWebRequests(string json)
    {
        Assert.False(DesktopBridgeProtocol.TryParseRequest(json, out _, out _));
    }

    [Fact]
    public void AcceptsOnlyEmptyGdacsRefreshPayload()
    {
        Assert.True(DesktopBridgeProtocol.TryParseRequest(
            """{"type":"providers.gdacs.refresh","payload":{}}""",
            out var request,
            out var error), error);
        Assert.Equal(DesktopBridgeProtocol.GdacsRefreshMessageType, request?.Type);

        Assert.False(DesktopBridgeProtocol.TryParseRequest(
            """{"type":"providers.gdacs.refresh","payload":{"endpoint":"https://example.com","from":"2020-01-01"}}""",
            out _,
            out _));
    }
    [Fact]
    public void ApplicationAssemblyIsAvailable()
    {
        Assert.Equal("GeopoliticalMonitor.Application", typeof(ApplicationAssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("Geopolitical Monitor", ProductIdentity.DisplayName);
    }

    [Fact]
    public void AcceptsFixedAppReadyMessage()
    {
        var accepted = DesktopBridgeProtocol.TryParseRequest(
            """{"type":"app.ready","payload":{}}""",
            out var request,
            out var error);

        Assert.True(accepted, error);
        Assert.Equal("app.ready", request?.Type);
    }

    [Fact]
    public void AcceptsPreferenceGetRequest()
    {
        var accepted = DesktopBridgeProtocol.TryParseRequest(
            """{"type":"preferences.get","payload":{"key":"AccentTheme"}}""",
            out var request,
            out var error);

        Assert.True(accepted, error);
        Assert.Equal(DesktopBridgeProtocol.PreferencesGetMessageType, request?.Type);
        Assert.Equal("AccentTheme", request?.PreferenceKey);
        Assert.Null(request?.PreferenceValue);
    }

    [Theory]
    [InlineData("Orange")]
    [InlineData("Blue")]
    [InlineData("Red")]
    [InlineData("Green")]
    [InlineData("Yellow")]
    public void AcceptsValidPreferenceSetRequest(string value)
    {
        var accepted = DesktopBridgeProtocol.TryParseRequest($"{{\"type\":\"preferences.set\",\"payload\":{{\"key\":\"AccentTheme\",\"value\":\"{value}\"}}}}",
            out var request,
            out var error);

        Assert.True(accepted, error);
        Assert.Equal(DesktopBridgeProtocol.PreferencesSetMessageType, request?.Type);
        Assert.Equal("AccentTheme", request?.PreferenceKey);
        Assert.Equal(value, request?.PreferenceValue);
    }

    [Fact]
    public void AcceptsEventsListRequest()
    {
        var accepted = DesktopBridgeProtocol.TryParseRequest(
            """{"type":"events.list","payload":{}}""",
            out var request,
            out var error);

        Assert.True(accepted, error);
        Assert.Equal(DesktopBridgeProtocol.EventsListMessageType, request?.Type);
    }

    [Fact]
    public void AcceptsEventsGetRequest()
    {
        var accepted = DesktopBridgeProtocol.TryParseRequest(
            """{"type":"events.get","payload":{"id":"evt-demo-baltic-security"}}""",
            out var request,
            out var error);

        Assert.True(accepted, error);
        Assert.Equal(DesktopBridgeProtocol.EventsGetMessageType, request?.Type);
        Assert.Equal("evt-demo-baltic-security", request?.EventId);
    }

    [Fact]
    public void AcceptsEventSourcesGetRequest()
    {
        var accepted = DesktopBridgeProtocol.TryParseRequest(
            """{"type":"events.sources.get","payload":{"id":"evt-demo-baltic-security"}}""",
            out var request,
            out var error);

        Assert.True(accepted, error);
        Assert.Equal(DesktopBridgeProtocol.EventSourcesGetMessageType, request?.Type);
        Assert.Equal("evt-demo-baltic-security", request?.EventId);
    }

    [Fact]
    public void AcceptsNarrowXPostAttachmentRequest()
    {
        var accepted = DesktopBridgeProtocol.TryParseRequest(
            """{"type":"events.sources.attachXPost","payload":{"id":"evt-demo","url":"https://x.com/example/status/1759288472993702104"}}""",
            out var request,
            out var error);

        Assert.True(accepted, error);
        Assert.Equal(DesktopBridgeProtocol.EventSourcesAttachXPostMessageType, request?.Type);
        Assert.Equal("evt-demo", request?.EventId);
        Assert.Equal("https://x.com/example/status/1759288472993702104", request?.XPostUrl);
    }

    [Theory]
    [InlineData("""{"type":"events.get","payload":{"id":""}}""")]
    [InlineData("""{"type":"events.get","payload":{"id":"evt demo"}}""")]
    [InlineData("""{"type":"events.sources.get","payload":{"id":42}}""")]
    [InlineData("""{"type":"events.sources.get","payload":{"id":"evt-demo","sql":"select 1"}}""")]
    [InlineData("""{"type":"events.sources.attachXPost","payload":{"id":"evt demo","url":"https://x.com/example/status/1"}}""")]
    [InlineData("""{"type":"events.sources.attachXPost","payload":{"id":"evt-demo","url":42}}""")]
    [InlineData("""{"type":"events.sources.attachXPost","payload":{"id":"evt-demo","url":""}}""")]
    [InlineData("""{"type":"events.sources.attachXPost","payload":{"id":"evt-demo","url":"https://x.com/example/status/1","sql":"select 1"}}""")]
    [InlineData("""{"type":"events.list","payload":{"id":"evt-demo"}}""")]
    public void RejectsMalformedEventMessages(string message)
    {
        Assert.False(DesktopBridgeProtocol.TryParseRequest(message, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void RejectsUnknownMessageType()
    {
        Assert.False(DesktopBridgeProtocol.TryParseRequest(
            """{"type":"native.execute","payload":{}}""",
            out _,
            out var error));
        Assert.Contains("Unsupported message type", error);
    }

    [Theory]
    [InlineData("""{"type":"app.ready","payload":{"path":"C:\\temp"}}""")]
    [InlineData("""{"type":"app.ready","unexpected":true}""")]
    [InlineData("""[]""")]
    [InlineData("""{"type":"preferences.get","payload":{"key":"Unknown"}}""")]
    [InlineData("""{"type":"preferences.get","payload":{"key":"AccentTheme","value":"Blue"}}""")]
    [InlineData("""{"type":"preferences.set","payload":{"key":"AccentTheme","value":"Purple"}}""")]
    [InlineData("""{"type":"preferences.set","payload":{"key":"AccentTheme","value":"Default"}}""")]
    [InlineData("""{"type":"preferences.set","payload":{"key":"AccentTheme","value":"red"}}""")]
    [InlineData("""{"type":"preferences.set","payload":{"key":"Unknown","value":"Blue"}}""")]
    [InlineData("""{"type":"preferences.set","payload":{"key":"AccentTheme"}}""")]
    [InlineData("""{"type":"preferences.set","payload":{"key":"AccentTheme","value":"Blue","sql":"select 1"}}""")]
    public void RejectsMalformedMessages(string message)
    {
        Assert.False(DesktopBridgeProtocol.TryParseRequest(message, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void CreatesSafeAppInfoResponse()
    {
        var json = DesktopBridgeProtocol.CreateAppInfoResponseJson(
            new DesktopBridgeAppInfo(
                ProductIdentity.DisplayName,
                "WinUI 3",
                "WebView2 desktop host",
                "SQLite local application data",
                false,
                XPaneSide.Left));

        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement.GetProperty("payload");

        Assert.Equal("app.info", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(ProductIdentity.DisplayName, payload.GetProperty("applicationName").GetString());
        Assert.Equal("WinUI 3", payload.GetProperty("shell").GetString());
        Assert.Equal("WebView2 desktop host", payload.GetProperty("uiHost").GetString());
        Assert.Equal("SQLite local application data", payload.GetProperty("persistence").GetString());
        Assert.False(payload.GetProperty("productionInboundPortsRequired").GetBoolean());
        Assert.Equal("Left", payload.GetProperty("xPaneSide").GetString());
        Assert.Equal(JsonValueKind.False, payload.GetProperty("demoEventsRemoved").ValueKind);
        Assert.Equal(7, payload.EnumerateObject().Count());
    }

    [Fact]
    public void AppInfoReportsDemoEventRemovalAsTypedBoolean()
    {
        var json = DesktopBridgeProtocol.CreateAppInfoResponseJson(
            new DesktopBridgeAppInfo(
                ProductIdentity.DisplayName,
                "WinUI 3",
                "WebView2 desktop host",
                "SQLite local application data",
                false,
                XPaneSide.Right,
                DemoEventsRemoved: true));

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("payload").GetProperty("demoEventsRemoved").GetBoolean());
    }

    [Fact]
    public void CreatesEventResponses()
    {
        var location = new LocationDto(57.4, 24.8, "Northern corridor", "DE", "Baltic demo region");
        var eventDto = new EventDto("evt-demo", "Demo event", "Demo summary", "Security", "Active", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, location, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 2, "High", "gdacs-eq-v1", "alert=Orange; magnitude=6.4", "Gdacs");
        var sourceDto = new SourceItemDto("src-demo", "XPost", "src-demo", "Demo source", "Demo text", null, "Demo Author", "@demo", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

        using var eventsDocument = JsonDocument.Parse(DesktopBridgeProtocol.CreateEventsListResultResponseJson([eventDto]));
        Assert.Equal(DesktopBridgeProtocol.EventsListResultMessageType, eventsDocument.RootElement.GetProperty("type").GetString());
        var listedEvent = eventsDocument.RootElement.GetProperty("payload").GetProperty("events")[0];
        Assert.Equal("evt-demo", listedEvent.GetProperty("id").GetString());
        Assert.Equal("High", listedEvent.GetProperty("severity").GetString());
        Assert.Equal("gdacs-eq-v1", listedEvent.GetProperty("severityRule").GetString());
        Assert.Equal("alert=Orange; magnitude=6.4", listedEvent.GetProperty("severityInputs").GetString());
        Assert.Equal("Gdacs", listedEvent.GetProperty("provider").GetString());
        Assert.False(listedEvent.TryGetProperty("providerKey", out _));

        using var eventDocument = JsonDocument.Parse(DesktopBridgeProtocol.CreateEventGetResultResponseJson(eventDto));
        Assert.Equal("High", eventDocument.RootElement.GetProperty("payload").GetProperty("eventItem").GetProperty("severity").GetString());

        using var sourcesDocument = JsonDocument.Parse(DesktopBridgeProtocol.CreateEventSourcesResultResponseJson("evt-demo", [sourceDto]));
        Assert.Equal(DesktopBridgeProtocol.EventSourcesResultMessageType, sourcesDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal("XPost", sourcesDocument.RootElement.GetProperty("payload").GetProperty("sources")[0].GetProperty("sourceType").GetString());

        var attachment = new AttachXPostSourceResult(AttachXPostSourceDisposition.CreatedAndAttached, sourceDto);
        using var attachmentDocument = JsonDocument.Parse(DesktopBridgeProtocol.CreateEventSourcesAttachXPostResultResponseJson("evt-demo", attachment));
        var attachmentPayload = attachmentDocument.RootElement.GetProperty("payload");
        Assert.Equal(DesktopBridgeProtocol.EventSourcesAttachXPostResultMessageType, attachmentDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal("CreatedAndAttached", attachmentPayload.GetProperty("disposition").GetString());
        Assert.Equal("src-demo", attachmentPayload.GetProperty("source").GetProperty("id").GetString());
    }

    [Fact]
    public void CreatesXPaneSideResponse()
    {
        using var document = JsonDocument.Parse(DesktopBridgeProtocol.CreateXPaneSideValueResponseJson(XPaneSide.Left));

        Assert.Equal(DesktopBridgeProtocol.XPaneSideValueMessageType, document.RootElement.GetProperty("type").GetString());
        Assert.Equal("Left", document.RootElement.GetProperty("payload").GetProperty("value").GetString());
    }

    [Theory]
    [InlineData(Preferences.AccentTheme.Orange, "Orange")]
    [InlineData(Preferences.AccentTheme.Blue, "Blue")]
    [InlineData(Preferences.AccentTheme.Red, "Red")]
    [InlineData(Preferences.AccentTheme.Green, "Green")]
    [InlineData(Preferences.AccentTheme.Yellow, "Yellow")]
    public void CreatesPreferenceResponses(Preferences.AccentTheme accentTheme, string expectedValue)
    {
        using var valueDocument = JsonDocument.Parse(DesktopBridgeProtocol.CreatePreferenceValueResponseJson(accentTheme));
        Assert.Equal(DesktopBridgeProtocol.PreferencesValueMessageType, valueDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal("AccentTheme", valueDocument.RootElement.GetProperty("payload").GetProperty("key").GetString());
        Assert.Equal(expectedValue, valueDocument.RootElement.GetProperty("payload").GetProperty("value").GetString());

        using var updatedDocument = JsonDocument.Parse(DesktopBridgeProtocol.CreatePreferenceUpdatedResponseJson(accentTheme));
        Assert.Equal(DesktopBridgeProtocol.PreferencesUpdatedMessageType, updatedDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(expectedValue, updatedDocument.RootElement.GetProperty("payload").GetProperty("value").GetString());
    }
}
