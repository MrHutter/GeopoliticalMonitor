using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XBrowserExtensionCapabilityDiagnosticTests
{
    [Fact]
    public void AvailableCapabilityRecordsSdkRuntimeAndApiAvailability()
    {
        var capability = XBrowserExtensionCapability.Available("1.0.3179.45", "153.0.4234.32");

        Assert.True(capability.IsAvailable);
        Assert.Equal(XBrowserExtensionFailureKind.None, capability.Diagnostic.FailureKind);
        Assert.Equal("1.0.3179.45", capability.Diagnostic.SdkVersion);
        Assert.Equal("153.0.4234.32", capability.Diagnostic.RuntimeVersion);
        Assert.Equal("available", capability.Diagnostic.ExtensionApi);
    }

    [Fact]
    public void FailureDiagnosticKeepsSafeBoundedDetails()
    {
        var exception = new HResultException(unchecked((int)0x80070032));

        var diagnostic = XBrowserExtensionCapabilityDiagnostic.FromException(
            "1.0.3179.45",
            "153.0.4234.32",
            "Profile.GetBrowserExtensionsAsync",
            exception);

        Assert.Equal(XBrowserExtensionFailureKind.NotSupported, diagnostic.FailureKind);
        Assert.Equal("0x80070032", diagnostic.HResult);
        Assert.Contains("WebView2 SDK: 1.0.3179.45", diagnostic.ToUnavailableMessage());
        Assert.Contains("WebView2 Runtime: 153.0.4234.32", diagnostic.ToUnavailableMessage());
        Assert.Contains("Extension API: unavailable", diagnostic.ToUnavailableMessage());
        Assert.DoesNotContain("cookie", diagnostic.ToUnavailableMessage(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", diagnostic.ToUnavailableMessage(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidStateHResultIsClassifiedExplicitly()
    {
        var exception = new HResultException(unchecked((int)0x8007139F));

        var diagnostic = XBrowserExtensionCapabilityDiagnostic.FromException(
            "1.0.3179.45",
            "153.0.4234.32",
            "CoreWebView2Environment.CreateAsync",
            exception);

        Assert.Equal(XBrowserExtensionFailureKind.InvalidState, diagnostic.FailureKind);
    }

    [Fact]
    public void UnsupportedHostDiagnosticDoesNotPretendAProbeRan()
    {
        var diagnostic = XBrowserExtensionCapabilityDiagnostic.UnsupportedHost(
            "1.0.3179.45",
            "153.0.4234.32");

        Assert.Equal(XBrowserExtensionFailureKind.NotSupported, diagnostic.FailureKind);
        Assert.Null(diagnostic.ExceptionType);
        Assert.Null(diagnostic.HResult);
        Assert.Equal("WinUI 3 WebView2 host policy", diagnostic.Stage);
        Assert.Contains("current WinUI 3 WebView2 host", diagnostic.ToUnavailableMessage());
    }

    private sealed class HResultException : Exception
    {
        public HResultException(int hresult)
            : base("Synthetic WebView2 failure")
        {
            HResult = hresult;
        }
    }
}

