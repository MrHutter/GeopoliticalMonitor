using GeopoliticalMonitor.Application.Preferences;

namespace GeopoliticalMonitor.Application.DesktopBridge;

public sealed record DesktopBridgeAppInfo(
    string ApplicationName,
    string Shell,
    string UiHost,
    string Persistence,
    bool ProductionInboundPortsRequired,
    XPaneSide XPaneSide,
    bool DemoEventsRemoved = false);
