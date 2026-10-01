using System.Text.Json;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Application.Refresh;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class ProviderRefreshStatusBridgeTests
{
    private static readonly DateTimeOffset LastSuccess = new(2026, 9, 30, 12, 1, 0, TimeSpan.Zero);

    [Fact]
    public void ParsesTheFieldlessStatusGetRequest()
    {
        Assert.True(DesktopBridgeProtocol.TryParseRequest("""{"type":"providers.refresh.status.get","payload":{}}""", out var request, out _));
        Assert.Equal(DesktopBridgeProtocol.ProvidersRefreshStatusGetMessageType, request!.Type);
        Assert.True(DesktopBridgeProtocol.TryParseRequest("""{"type":"providers.refresh.status.get"}""", out _, out _));
    }

    [Theory]
    [InlineData("""{"type":"providers.refresh.status.get","payload":{"provider":"Gdacs"}}""")]
    [InlineData("""{"type":"providers.refresh.status.get","payload":{"x":1,"x":1}}""")]
    [InlineData("""{"type":"providers.refresh.status.get","payload":{},"payload":{}}""")]
    [InlineData("""{"type":"providers.refresh.status.get","type":"providers.refresh.status.get","payload":{}}""")]
    [InlineData("""{"type":"providers.refresh.status.get","payload":{},"extra":true}""")]
    [InlineData("""{"type":"providers.refresh.status","payload":{}}""")]
    [InlineData("""{"type":"providers.refresh.status.set","payload":{}}""")]
    [InlineData("""{"type":"providers.gdacs.refresh","payload":{"interval":1}}""")]
    public void RejectsUnexpectedDuplicateAndUnknownStatusRequests(string json)
    {
        Assert.False(DesktopBridgeProtocol.TryParseRequest(json, out var request, out var error));
        Assert.Null(request);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void RejectsDuplicateTopLevelFieldsForExistingRequests()
    {
        Assert.False(DesktopBridgeProtocol.TryParseRequest("""{"type":"app.ready","type":"events.list","payload":{}}""", out _, out var error));
        Assert.Contains("Duplicate", error);
    }

    [Fact]
    public void SerializesOnlyTheApprovedStatusFields()
    {
        var json = DesktopBridgeProtocol.CreateProvidersRefreshStatusJson(
        [
            new ProviderRefreshStatus(ScheduledProviderId.Gdacs, "GDACS", ProviderRefreshState.Failed, LastSuccess, LastSuccess.AddMinutes(30), ProviderStatusMessage.TimedOut, 0),
        ]);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(["payload", "type"], root.EnumerateObject().Select(item => item.Name).Order());
        Assert.Equal("providers.refresh.status", root.GetProperty("type").GetString());
        Assert.Equal(["providers"], root.GetProperty("payload").EnumerateObject().Select(item => item.Name));
        var provider = Assert.Single(root.GetProperty("payload").GetProperty("providers").EnumerateArray().ToArray());
        Assert.Equal(
            ["displayName", "lastSuccessAtUtc", "message", "newAlerts", "nextRefreshAtUtc", "provider", "state"],
            provider.EnumerateObject().Select(item => item.Name).Order());
        Assert.Equal("Gdacs", provider.GetProperty("provider").GetString());
        Assert.Equal("GDACS", provider.GetProperty("displayName").GetString());
        Assert.Equal("Failed", provider.GetProperty("state").GetString());
        Assert.Equal("Request timed out. Existing events were preserved.", provider.GetProperty("message").GetString());
        Assert.Equal(LastSuccess, provider.GetProperty("lastSuccessAtUtc").GetDateTimeOffset());
        Assert.Equal(0, provider.GetProperty("newAlerts").GetInt32());
        Assert.DoesNotContain("http", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializesNullTimesAndMessageForANewProvider()
    {
        var json = DesktopBridgeProtocol.CreateProvidersRefreshStatusJson(
            [new ProviderRefreshStatus(ScheduledProviderId.Gdacs, "GDACS", ProviderRefreshState.Idle, null, null, ProviderStatusMessage.None, 0)]);

        using var document = JsonDocument.Parse(json);
        var provider = document.RootElement.GetProperty("payload").GetProperty("providers")[0];
        Assert.Equal(JsonValueKind.Null, provider.GetProperty("lastSuccessAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, provider.GetProperty("nextRefreshAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, provider.GetProperty("message").ValueKind);
        Assert.Equal("""{"type":"providers.refresh.status","payload":{"providers":[]}}""", DesktopBridgeProtocol.CreateProvidersRefreshStatusJson([]));
    }

    [Fact]
    public void EveryStatusMessageMapsToAFixedShortText()
    {
        foreach (var message in Enum.GetValues<ProviderStatusMessage>())
        {
            var text = ProviderStatusMessages.ToText(message);
            if (message == ProviderStatusMessage.None)
            {
                Assert.Null(text);
                continue;
            }

            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.True(text!.Length <= 80);
            Assert.DoesNotContain("hybrid", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task StatusGetRequestIsDispatchedOnlyForTheTrustedOrigin()
    {
        const string json = """{"type":"providers.refresh.status.get","payload":{}}""";
        var dispatched = new List<string>();

        var trusted = await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin, json, Record);
        var untrusted = await PrivilegedBridgeMessageProcessor.TryProcessAsync("https://untrusted.invalid/", json, Record);
        var xOrigin = await PrivilegedBridgeMessageProcessor.TryProcessAsync("https://x.com/home", json, Record);
        var duplicate = await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            PrivilegedWebViewSecurityPolicy.TrustedOrigin,
            """{"type":"providers.refresh.status.get","payload":{},"payload":{}}""",
            Record);
        var unexpected = await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            PrivilegedWebViewSecurityPolicy.TrustedOrigin,
            """{"type":"providers.refresh.status.get","payload":{"state":"Idle"}}""",
            Record);
        var unknown = await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            PrivilegedWebViewSecurityPolicy.TrustedOrigin,
            """{"type":"providers.refresh.status","payload":{}}""",
            Record);

        Assert.True(trusted);
        Assert.False(untrusted);
        Assert.False(xOrigin);
        Assert.False(duplicate);
        Assert.False(unexpected);
        Assert.False(unknown);
        Assert.Equal([DesktopBridgeProtocol.ProvidersRefreshStatusGetMessageType], dispatched);

        Task Record(DesktopBridgeRequest request)
        {
            dispatched.Add(request.Type);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GdacsHandlerCanRouteThroughADelegateAndKeepsItsResultShape()
    {
        var calls = 0;
        var handler = new GdacsRefreshBridgeHandler(_ =>
        {
            calls++;
            return Task.FromResult(new GdacsRefreshResult(GdacsRefreshStatus.Succeeded, NewEvents: 1, NewAlerts: 1));
        });

        using var document = JsonDocument.Parse(await handler.RefreshAsync());

        Assert.Equal(1, calls);
        Assert.Equal(DesktopBridgeProtocol.GdacsRefreshResultMessageType, document.RootElement.GetProperty("type").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("payload").GetProperty("newAlerts").GetInt32());
    }
}
