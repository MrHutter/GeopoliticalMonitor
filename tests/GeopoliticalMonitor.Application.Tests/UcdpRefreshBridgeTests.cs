using System.Text.Json;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.Refresh;
using GeopoliticalMonitor.Application.Ucdp;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class UcdpRefreshBridgeTests
{
    private static readonly DateTimeOffset LastSuccess = new(2026, 10, 2, 9, 2, 0, TimeSpan.Zero);

    [Fact]
    public void ParsesTheFieldlessUcdpRefreshRequest()
    {
        Assert.True(DesktopBridgeProtocol.TryParseRequest("""{"type":"providers.ucdp.refresh","payload":{}}""", out var request, out _));
        Assert.Equal(DesktopBridgeProtocol.UcdpRefreshMessageType, request!.Type);
        Assert.True(DesktopBridgeProtocol.TryParseRequest("""{"type":"providers.ucdp.refresh"}""", out _, out _));
    }

    [Theory]
    [InlineData("""{"type":"providers.ucdp.refresh","payload":{"release":"26.0.8"}}""")]
    [InlineData("""{"type":"providers.ucdp.refresh","payload":{"url":"https://ucdp.uu.se/downloads/candidateged/GEDEvent_v26_0_8.csv"}}""")]
    [InlineData("""{"type":"providers.ucdp.refresh","payload":{"x":1,"x":1}}""")]
    [InlineData("""{"type":"providers.ucdp.refresh","payload":{},"payload":{}}""")]
    [InlineData("""{"type":"providers.ucdp.refresh","type":"providers.ucdp.refresh","payload":{}}""")]
    [InlineData("""{"type":"providers.ucdp.refresh","payload":{},"extra":true}""")]
    [InlineData("""{"type":"providers.ucdp.refresh","payload":[]}""")]
    [InlineData("""{"type":"providers.ucdp.refresh.result","payload":{}}""")]
    [InlineData("""{"type":"providers.ucdp.download","payload":{}}""")]
    public void RejectsFieldsDuplicatesAndUnknownUcdpTypes(string json)
    {
        Assert.False(DesktopBridgeProtocol.TryParseRequest(json, out var request, out var error));
        Assert.Null(request);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task ProcessorDispatchesOnlyTrustedValidUcdpRequests()
    {
        var dispatched = new List<string>();
        Task Dispatch(DesktopBridgeRequest request)
        {
            dispatched.Add(request.Type);
            return Task.CompletedTask;
        }

        Assert.True(await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin + "/index.html", """{"type":"providers.ucdp.refresh","payload":{}}""", Dispatch));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync("https://x.com/home", """{"type":"providers.ucdp.refresh","payload":{}}""", Dispatch));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync("https://untrusted.invalid/", "not json at all", Dispatch));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin, """{"type":"providers.ucdp.refresh","payload":{"force":true}}""", Dispatch));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin, """{"type":"providers.ucdp.refresh","payload":{"a":1,"a":2}}""", Dispatch));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin, """{"type":"providers.ucdp.refreshAll","payload":{}}""", Dispatch));

        Assert.Equal([DesktopBridgeProtocol.UcdpRefreshMessageType], dispatched);
    }

    [Fact]
    public async Task HandlerReturnsTypedResultsAndFixedErrorMessages()
    {
        var success = await new UcdpRefreshBridgeHandler(_ => Task.FromResult(new UcdpRefreshResult(
            UcdpRefreshStatus.Succeeded, 202608, ReleasesImported: 2, NewEvents: 5, SkippedZeroFatalities: 3, DroppedByCap: 1))).RefreshAsync();
        using (var document = JsonDocument.Parse(success))
        {
            Assert.Equal("providers.ucdp.refresh.result", document.RootElement.GetProperty("type").GetString());
            var payload = document.RootElement.GetProperty("payload");
            Assert.Equal(
                ["conflicts", "droppedByCap", "newestRelease", "newEvents", "releasesImported", "skippedImpreciseLocation", "skippedInvalidCoordinates", "skippedMalformedRecords", "skippedOutsideWindow", "skippedZeroFatalities", "status", "unchangedRecords", "updatedEvents"],
                payload.EnumerateObject().Select(item => item.Name).Order());
            Assert.Equal("Succeeded", payload.GetProperty("status").GetString());
            Assert.Equal("2026-08", payload.GetProperty("newestRelease").GetString());
            Assert.Equal(5, payload.GetProperty("newEvents").GetInt32());
        }

        var none = await new UcdpRefreshBridgeHandler(_ => Task.FromResult(new UcdpRefreshResult(UcdpRefreshStatus.NoNewRelease))).RefreshAsync();
        using (var document = JsonDocument.Parse(none))
        {
            Assert.Equal("NoNewRelease", document.RootElement.GetProperty("payload").GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("payload").GetProperty("newestRelease").ValueKind);
        }

        var failure = await new UcdpRefreshBridgeHandler(_ => Task.FromResult(new UcdpRefreshResult(UcdpRefreshStatus.TimedOut))).RefreshAsync();
        using (var document = JsonDocument.Parse(failure))
        {
            Assert.Equal("providers.ucdp.refresh.error", document.RootElement.GetProperty("type").GetString());
            Assert.Equal("The UCDP request timed out. Existing events were preserved.", document.RootElement.GetProperty("payload").GetProperty("message").GetString());
            Assert.DoesNotContain("http", failure, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void StatusPayloadAddsOptionalFieldsOnlyForUcdp()
    {
        var json = DesktopBridgeProtocol.CreateProvidersRefreshStatusJson(
        [
            new ProviderRefreshStatus(ScheduledProviderId.Gdacs, "GDACS", ProviderRefreshState.Idle, LastSuccess, LastSuccess.AddMinutes(30), ProviderStatusMessage.UpToDate, 0),
            new ProviderRefreshStatus(ScheduledProviderId.UcdpCandidate, "UCDP Candidate", ProviderRefreshState.Idle, LastSuccess, LastSuccess.AddHours(6), ProviderStatusMessage.NoNewRelease, 0, "monthly release", "2026-08"),
        ]);

        using var document = JsonDocument.Parse(json);
        var providers = document.RootElement.GetProperty("payload").GetProperty("providers").EnumerateArray().ToArray();
        Assert.Equal(
            ["displayName", "lastSuccessAtUtc", "message", "newAlerts", "nextRefreshAtUtc", "provider", "state"],
            providers[0].EnumerateObject().Select(item => item.Name).Order());
        Assert.Equal(
            ["cadenceLabel", "dataThrough", "displayName", "lastSuccessAtUtc", "message", "newAlerts", "nextRefreshAtUtc", "provider", "state"],
            providers[1].EnumerateObject().Select(item => item.Name).Order());
        Assert.Equal("UcdpCandidate", providers[1].GetProperty("provider").GetString());
        Assert.Equal("No new release", providers[1].GetProperty("message").GetString());
        Assert.Equal("monthly release", providers[1].GetProperty("cadenceLabel").GetString());
        Assert.Equal("2026-08", providers[1].GetProperty("dataThrough").GetString());
        Assert.Equal(LastSuccess, providers[1].GetProperty("lastSuccessAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public void EventDtoCarriesTheReleaseOnlyWhenPresent()
    {
        var location = new LocationDto(15.5, 32.5, "Khartoum town, Sudan", null, "Africa");
        var ucdp = new EventDto("evt-ucdp-cand-1", "T", "S", "MilitaryConflict", "Unverified", LastSuccess, LastSuccess, location, LastSuccess, LastSuccess, 1, "High", "ucdp-candidate-v1", "best=12; low=10; high=15", "UcdpCandidate", 202608);
        var other = ucdp with { Id = "evt-one", Provider = "Manual", ProviderLastSeenRelease = null };

        using var withRelease = JsonDocument.Parse(DesktopBridgeProtocol.CreateEventGetResultResponseJson(ucdp));
        using var withoutRelease = JsonDocument.Parse(DesktopBridgeProtocol.CreateEventGetResultResponseJson(other));

        var ucdpEvent = withRelease.RootElement.GetProperty("payload").GetProperty("eventItem");
        Assert.Equal(202608, ucdpEvent.GetProperty("providerLastSeenRelease").GetInt32());
        Assert.False(withoutRelease.RootElement.GetProperty("payload").GetProperty("eventItem").TryGetProperty("providerLastSeenRelease", out _));
    }
}
