using System.Text.Json;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Firms;

namespace GeopoliticalMonitor.Application.Tests;

public class FirmsBridgeTests
{
    [Fact]
    public void ProtocolAcceptsOnlyExactFiniteViewportShape()
    {
        Assert.True(DesktopBridgeProtocol.TryParseRequest(
            """{"type":"providers.firms.refresh","payload":{"west":170,"south":-10,"east":-170,"north":20}}""",
            out var request,
            out var error), error);
        Assert.Equal(170, request!.FirmsWest);
        Assert.Equal(-170, request.FirmsEast);

        Assert.False(DesktopBridgeProtocol.TryParseRequest(
            """{"type":"providers.firms.refresh","payload":{"west":0,"south":0,"east":10,"north":10,"url":"https://example.com","mapKey":"unit-test-only-map-key"}}""",
            out _, out _));
        Assert.False(DesktopBridgeProtocol.TryParseRequest(
            """{"type":"providers.firms.refresh","payload":{"west":0,"west":1,"south":0,"east":10,"north":10}}""",
            out _, out _));
        Assert.False(DesktopBridgeProtocol.TryParseRequest(
            """{"type":"providers.firms.refresh","payload":{"west":0,"south":0,"east":10}}""",
            out _, out _));
        Assert.False(DesktopBridgeProtocol.TryParseRequest(
            """{"type":"providers.firms.refresh","payload":{"west":"0","south":0,"east":10,"north":10}}""",
            out _, out _));
    }

    [Fact]
    public async Task ExistingOriginProcessorGuardsFirmsRequest()
    {
        var calls = 0;
        var json = """{"type":"providers.firms.refresh","payload":{"west":0,"south":0,"east":10,"north":10}}""";

        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            "https://evil.example", json, _ => { calls++; return Task.CompletedTask; }));
        Assert.True(await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            PrivilegedWebViewSecurityPolicy.TrustedOrigin, json, _ => { calls++; return Task.CompletedTask; }));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task HandlerSerializesBoundedResultWithoutCredentialOrUrl()
    {
        var now = new DateTimeOffset(2026, 9, 28, 15, 0, 0, TimeSpan.Zero);
        var feed = new FixedFeed(now);
        var handler = new FirmsRefreshBridgeHandler(new FirmsRefreshService(feed, new FixedTimeProvider(now)));
        var request = new DesktopBridgeRequest(
            DesktopBridgeProtocol.FirmsRefreshMessageType,
            FirmsWest: 0, FirmsSouth: 0, FirmsEast: 10, FirmsNorth: 10);

        var json = await handler.RefreshAsync(request);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(DesktopBridgeProtocol.FirmsRefreshResultMessageType, document.RootElement.GetProperty("type").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("payload").GetProperty("observationCount").GetInt32());
        Assert.DoesNotContain("map-key", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api/area", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ErrorSerializationContainsOnlyStatusAndSafeMessage()
    {
        var json = DesktopBridgeProtocol.CreateFirmsRefreshErrorResponseJson(
            FirmsRefreshStatus.ConfigurationUnavailable,
            "NASA FIRMS is not configured.");
        using var document = JsonDocument.Parse(json);
        var payload = document.RootElement.GetProperty("payload");

        Assert.Equal(2, payload.EnumerateObject().Count());
        Assert.Equal("ConfigurationUnavailable", payload.GetProperty("status").GetString());
        Assert.DoesNotContain("map", payload.GetProperty("message").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixedFeed(DateTimeOffset now) : IFirmsObservationFeed
    {
        public Task<FirmsObservationBatch> FetchAsync(FirmsSource source, FirmsBoundingBox bounds, CancellationToken cancellationToken = default)
        {
            var provider = source.ToProviderValue();
            var satellite = source == FirmsSource.ViirsNoaa20Nrt ? "1" : "2";
            var observation = new FirmsObservation(
                FirmsObservationIdentity.Create(provider, satellite, now.AddMinutes(-5), 1, 2),
                1, 2, now.AddMinutes(-5), provider, satellite, "Nominal", 2, 330, 290, 0.4, 0.5, "2.0NRT", "Day");
            return Task.FromResult(new FirmsObservationBatch([observation], 0));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
