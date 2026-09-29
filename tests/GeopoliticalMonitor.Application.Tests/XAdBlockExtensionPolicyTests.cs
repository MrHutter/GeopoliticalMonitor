using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XAdBlockExtensionPolicyTests
{
    [Theory]
    [InlineData(XAdBlockExtensionPolicy.ChromeExtensionId)]
    [InlineData(XAdBlockExtensionPolicy.EdgeExtensionId)]
    public void RecognizesKnownStoreExtensionIds(string extensionId)
    {
        Assert.True(XAdBlockExtensionPolicy.IsKnownStoreId(extensionId));
    }

    [Theory]
    [InlineData("cfhdojbkjhnklbpkdaibdccddilifddb")]
    [InlineData("hpgebfmcjdefclbkpgkdlbkbpkjjeofb")]
    [InlineData("different-extension")]
    [InlineData("")]
    [InlineData(null)]
    public void DoesNotTreatPathDerivedOrOtherIdsAsKnownStoreIds(string? extensionId)
    {
        Assert.False(XAdBlockExtensionPolicy.IsKnownStoreId(extensionId));
    }
}
