namespace GeopoliticalMonitor.Application.XBrowser;

public enum XToolbarCommand
{
    Back,
    Forward,
    Home,
    Refresh,
    OpenExternal,
    CopyPostLink,
    ClearSession,
    AdBlocker,
}

public enum XToolbarCommandPlacement
{
    Primary,
    Overflow,
}

public sealed record XToolbarCommandRule(XToolbarCommand Command, XToolbarCommandPlacement Placement);

public static class XToolbarPolicy
{
    public static IReadOnlyList<XToolbarCommandRule> CommandRules { get; } =
    [
        new(XToolbarCommand.Back, XToolbarCommandPlacement.Primary),
        new(XToolbarCommand.Forward, XToolbarCommandPlacement.Primary),
        new(XToolbarCommand.Home, XToolbarCommandPlacement.Primary),
        new(XToolbarCommand.Refresh, XToolbarCommandPlacement.Primary),
        new(XToolbarCommand.OpenExternal, XToolbarCommandPlacement.Overflow),
        new(XToolbarCommand.CopyPostLink, XToolbarCommandPlacement.Overflow),
        new(XToolbarCommand.ClearSession, XToolbarCommandPlacement.Overflow),
        new(XToolbarCommand.AdBlocker, XToolbarCommandPlacement.Overflow),
    ];
}