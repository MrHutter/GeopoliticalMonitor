using GeopoliticalMonitor.Application.DesktopBridge;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class PrivilegedBridgeMessageProcessorTests
{
    [Fact]
    public async Task TrustedSourceDispatchesValidatedTypedRequest()
    {
        DesktopBridgeRequest? dispatchedRequest = null;
        var dispatchCount = 0;

        var processed = await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            PrivilegedWebViewSecurityPolicy.TrustedOrigin + "/index.html",
            """{"type":"app.ready","payload":{}}""",
            request =>
            {
                dispatchCount++;
                dispatchedRequest = request;
                return Task.CompletedTask;
            });

        Assert.True(processed);
        Assert.Equal(1, dispatchCount);
        Assert.Equal(DesktopBridgeProtocol.AppReadyMessageType, dispatchedRequest?.Type);
    }

    [Fact]
    public async Task UntrustedSourceIsRejectedWithoutDispatch()
    {
        var dispatchCount = 0;

        var processed = await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            "https://untrusted.invalid/",
            """{"type":"app.ready","payload":{}}""",
            _ =>
            {
                dispatchCount++;
                return Task.CompletedTask;
            });

        Assert.False(processed);
        Assert.Equal(0, dispatchCount);
    }

    [Fact]
    public async Task MalformedSourceIsRejectedWithoutDispatch()
    {
        var dispatchCount = 0;

        var processed = await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            "not a uri",
            """{"type":"app.ready","payload":{}}""",
            _ =>
            {
                dispatchCount++;
                return Task.CompletedTask;
            });

        Assert.False(processed);
        Assert.Equal(0, dispatchCount);
    }

    [Fact]
    public async Task InvalidPayloadFromTrustedSourceIsRejectedWithoutDispatch()
    {
        var dispatchCount = 0;

        var processed = await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            PrivilegedWebViewSecurityPolicy.TrustedOrigin,
            """{"type":"app.ready","payload":{"unexpected":true}}""",
            _ =>
            {
                dispatchCount++;
                return Task.CompletedTask;
            });

        Assert.False(processed);
        Assert.Equal(0, dispatchCount);
    }

    [Fact]
    public async Task TrustedWatchlistCommandDispatchesButUntrustedSourceDoesNot()
    {
        const string json = """{"type":"watchlists.list","payload":{}}""";
        var dispatchCount = 0;

        var trusted = await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            PrivilegedWebViewSecurityPolicy.TrustedOrigin,
            json,
            request =>
            {
                Assert.Equal(DesktopBridgeProtocol.WatchlistsListMessageType, request.Type);
                dispatchCount++;
                return Task.CompletedTask;
            });
        var untrusted = await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            "https://untrusted.invalid/",
            json,
            _ =>
            {
                dispatchCount++;
                return Task.CompletedTask;
            });

        Assert.True(trusted);
        Assert.False(untrusted);
        Assert.Equal(1, dispatchCount);
    }

    [Fact]
    public async Task TrustedAlertCommandDispatchesButXOriginIsRejectedFirst()
    {
        const string json = """{"type":"alerts.summary","payload":{}}""";
        var dispatchCount = 0;

        var trusted = await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            PrivilegedWebViewSecurityPolicy.TrustedOrigin,
            json,
            request =>
            {
                Assert.Equal(DesktopBridgeProtocol.AlertsSummaryMessageType, request.Type);
                dispatchCount++;
                return Task.CompletedTask;
            });
        var xOrigin = await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            "https://x.com/home",
            json,
            _ =>
            {
                dispatchCount++;
                return Task.CompletedTask;
            });

        Assert.True(trusted);
        Assert.False(xOrigin);
        Assert.Equal(1, dispatchCount);
    }
}
