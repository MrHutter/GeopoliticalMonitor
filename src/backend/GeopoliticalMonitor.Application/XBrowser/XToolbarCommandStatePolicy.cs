namespace GeopoliticalMonitor.Application.XBrowser;

public enum XBrowserRuntimeState
{
    NotStarted,
    Initializing,
    Ready,
    Failed,
}

public sealed record XToolbarCommandState(XBrowserRuntimeState RuntimeState, bool CanGoBack, bool CanGoForward, string? CurrentUrl);

public static class XToolbarCommandStatePolicy
{
    public static bool IsEnabled(XToolbarCommand command, XToolbarCommandState state)
    {
        return command switch
        {
            XToolbarCommand.Back => state.RuntimeState == XBrowserRuntimeState.Ready && state.CanGoBack,
            XToolbarCommand.Forward => state.RuntimeState == XBrowserRuntimeState.Ready && state.CanGoForward,
            XToolbarCommand.Home => state.RuntimeState == XBrowserRuntimeState.Ready,
            XToolbarCommand.Refresh => state.RuntimeState == XBrowserRuntimeState.Ready,
            XToolbarCommand.OpenExternal => state.RuntimeState == XBrowserRuntimeState.Failed || XBrowserNavigationPolicy.IsAllowedInPane(state.CurrentUrl),
            XToolbarCommand.CopyPostLink => state.RuntimeState == XBrowserRuntimeState.Ready && XPostUrl.TryNormalize(state.CurrentUrl, out _),
            XToolbarCommand.ClearSession => state.RuntimeState == XBrowserRuntimeState.Ready,
            XToolbarCommand.AdBlocker => state.RuntimeState == XBrowserRuntimeState.Ready,
            _ => false,
        };
    }
}

public static class XOverflowPanelPolicy
{
    public const double DesiredWidth = 190;
    public const double MinimumWidth = 150;
    public const double HorizontalInset = 20;

    public static double GetPanelWidth(double availablePaneWidth)
    {
        if (availablePaneWidth <= 0)
        {
            return MinimumWidth;
        }

        var containedWidth = Math.Max(MinimumWidth, availablePaneWidth - HorizontalInset);
        return Math.Min(DesiredWidth, containedWidth);
    }
}