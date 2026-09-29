using GeopoliticalMonitor.Application.Preferences;

namespace GeopoliticalMonitor.Application.XBrowser;

public enum MainContentOrder
{
    EventsThenMap,
    MapThenEvents,
}

public sealed record XPaneArrangement(XPaneSide XPaneSide, MainContentOrder MainContentOrder, bool DetailsSpanMapAndEvents);

public static class XPaneLayoutPolicy
{
    public static XPaneArrangement Arrange(XPaneSide side)
    {
        return side switch
        {
            XPaneSide.Left => new XPaneArrangement(side, MainContentOrder.MapThenEvents, DetailsSpanMapAndEvents: true),
            _ => new XPaneArrangement(XPaneSide.Right, MainContentOrder.EventsThenMap, DetailsSpanMapAndEvents: true),
        };
    }
}
