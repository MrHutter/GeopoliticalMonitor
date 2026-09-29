using GeopoliticalMonitor.Application.DesktopBridge;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class PrivilegedWebViewSecurityPolicyTests
{
    [Theory]
    [InlineData(PrivilegedWebViewSecurityPolicy.TrustedOrigin)]
    [InlineData(PrivilegedWebViewSecurityPolicy.TrustedOrigin + "/index.html")]
    [InlineData(PrivilegedWebViewSecurityPolicy.TrustedOrigin + "/dashboard?range=24h#events")]
    [InlineData("https://app.geopoliticalmonitor.local:443/index.html")]
    public void AcceptsExactOriginAndSameOriginApplicationRoutes(string uri)
    {
        Assert.True(PrivilegedWebViewSecurityPolicy.IsTrustedOrigin(uri));
    }

    [Theory]
    [InlineData(PrivilegedWebViewSecurityPolicy.TrustedOrigin + ".evil.invalid/")]
    [InlineData("https://untrusted.invalid/")]
    [InlineData("http://app.geopoliticalmonitor.local/")]
    [InlineData("https://app.geopoliticalmonitor.local:444/")]
    [InlineData("https://app.geopoliticalmonitor.local@evil.invalid/")]
    [InlineData("not a uri")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsUrisOutsideTheExactTrustedOrigin(string? uri)
    {
        Assert.False(PrivilegedWebViewSecurityPolicy.IsTrustedOrigin(uri));
    }

    [Fact]
    public void AcceptsExistingBridgeRequestFromTrustedSource()
    {
        var accepted = PrivilegedWebViewSecurityPolicy.TryParseTrustedBridgeRequest(
            PrivilegedWebViewSecurityPolicy.TrustedOrigin + "/index.html",
            """{"type":"app.ready","payload":{}}""",
            out var request,
            out var error);

        Assert.True(accepted, error);
        Assert.Equal(DesktopBridgeProtocol.AppReadyMessageType, request?.Type);
    }

    [Fact]
    public void RejectsValidPrivilegedRequestFromUntrustedSource()
    {
        var accepted = PrivilegedWebViewSecurityPolicy.TryParseTrustedBridgeRequest(
            PrivilegedWebViewSecurityPolicy.TrustedOrigin + ".evil.invalid/",
            """{"type":"app.ready","payload":{}}""",
            out var request,
            out var error);

        Assert.False(accepted);
        Assert.Null(request);
        Assert.Equal("Bridge message source is not trusted.", error);
    }

    [Fact]
    public void RejectsUntrustedSourceBeforeParsingPayload()
    {
        var accepted = PrivilegedWebViewSecurityPolicy.TryParseTrustedBridgeRequest(
            "https://untrusted.invalid/",
            "not valid json",
            out var request,
            out var error);

        Assert.False(accepted);
        Assert.Null(request);
        Assert.Equal("Bridge message source is not trusted.", error);
    }
}
