namespace GeopoliticalMonitor.Application.XBrowser;

public sealed record XBrowserSecurityConfiguration(
    bool AreHostObjectsAllowed,
    bool IsWebMessageEnabled,
    bool IsPasswordAutosaveEnabled,
    bool IsGeneralAutofillEnabled,
    bool InjectsScripts,
    bool UsesDevToolsProtocol)
{
    public static XBrowserSecurityConfiguration IsolatedDefault { get; } = new(
        AreHostObjectsAllowed: false,
        IsWebMessageEnabled: false,
        IsPasswordAutosaveEnabled: false,
        IsGeneralAutofillEnabled: false,
        InjectsScripts: false,
        UsesDevToolsProtocol: false);
}