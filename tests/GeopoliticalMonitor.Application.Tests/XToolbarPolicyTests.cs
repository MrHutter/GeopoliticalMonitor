using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XToolbarPolicyTests
{
    [Fact]
    public void KeepsNavigationPrimaryAndMovesSecondaryActionsToOverflow()
    {
        var rules = XToolbarPolicy.CommandRules.ToDictionary(item => item.Command, item => item.Placement);

        Assert.Equal(XToolbarCommandPlacement.Primary, rules[XToolbarCommand.Back]);
        Assert.Equal(XToolbarCommandPlacement.Primary, rules[XToolbarCommand.Forward]);
        Assert.Equal(XToolbarCommandPlacement.Primary, rules[XToolbarCommand.Home]);
        Assert.Equal(XToolbarCommandPlacement.Primary, rules[XToolbarCommand.Refresh]);
        Assert.Equal(XToolbarCommandPlacement.Overflow, rules[XToolbarCommand.OpenExternal]);
        Assert.Equal(XToolbarCommandPlacement.Overflow, rules[XToolbarCommand.CopyPostLink]);
        Assert.Equal(XToolbarCommandPlacement.Overflow, rules[XToolbarCommand.ClearSession]);
        Assert.Equal(XToolbarCommandPlacement.Overflow, rules[XToolbarCommand.AdBlocker]);
    }
}