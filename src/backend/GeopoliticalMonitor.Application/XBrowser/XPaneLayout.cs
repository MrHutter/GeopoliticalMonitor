namespace GeopoliticalMonitor.Application.XBrowser;

public readonly record struct XPaneCollapseState(double RememberedWidth, bool ShouldPersist);

public static class XPaneLayout
{
    public static double GetDefaultPaneWidth(double availableWidth)
    {
        if (!IsFinitePositive(availableWidth))
        {
            return XBrowserDefaults.DefaultPaneWidth;
        }

        return ClampPaneWidth(availableWidth * XBrowserDefaults.DefaultPaneWidthRatio, availableWidth);
    }

    public static double ClampPaneWidth(double requestedWidth, double availableWidth)
    {
        if (!IsFinitePositive(requestedWidth))
        {
            return GetDefaultPaneWidth(availableWidth);
        }

        var maximumWidth = GetMaximumPaneWidth(availableWidth);
        var minimumWidth = Math.Min(XBrowserDefaults.MinimumPaneWidth, maximumWidth);
        return Math.Clamp(requestedWidth, minimumWidth, maximumWidth);
    }

    public static double GetMaximumPaneWidth(double availableWidth)
    {
        if (!IsFinitePositive(availableWidth))
        {
            return XBrowserDefaults.DefaultPaneWidth / XBrowserDefaults.DefaultPaneWidthRatio * XBrowserDefaults.MaximumPaneWidthRatio;
        }

        var ratioLimit = availableWidth * XBrowserDefaults.MaximumPaneWidthRatio;
        var mainPaneLimit = availableWidth - XBrowserDefaults.MinimumMainPaneWidth - XBrowserDefaults.SplitterWidth;
        return Math.Max(0, Math.Min(ratioLimit, mainPaneLimit));
    }

    public static double GetMainPaneWidth(double xPaneWidth, double availableWidth)
    {
        var clampedXPaneWidth = ClampPaneWidth(xPaneWidth, availableWidth);
        return Math.Max(0, availableWidth - XBrowserDefaults.SplitterWidth - clampedXPaneWidth);
    }

    public static XPaneCollapseState Collapse(double currentWidth, double rememberedWidth, double availableWidth)
    {
        var widthToRemember = IsFinitePositive(currentWidth)
            ? ClampPaneWidth(currentWidth, availableWidth)
            : ClampPaneWidth(rememberedWidth, availableWidth);

        return new XPaneCollapseState(widthToRemember, ShouldPersist: false);
    }

    public static double Reopen(double rememberedWidth, double availableWidth)
    {
        return ClampPaneWidth(rememberedWidth, availableWidth);
    }

    private static bool IsFinitePositive(double value)
    {
        return double.IsFinite(value) && value > 0;
    }
}