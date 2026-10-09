namespace GeopoliticalMonitor.Application;

public static class ProductIdentity
{
    public const string DisplayName = "Northern Monitor";
    public const string Version = "0.1.0";

    /// <summary>Outbound User-Agent: product name and version only, no user or machine identifiers.</summary>
    public const string UserAgent = "NorthernMonitor/" + Version;
}
