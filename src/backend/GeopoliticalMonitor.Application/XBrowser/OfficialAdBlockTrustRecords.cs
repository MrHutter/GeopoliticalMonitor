namespace GeopoliticalMonitor.Application.XBrowser;

public sealed record OfficialAdBlockTrustRecord(
    int TrustSchemaVersion,
    string ProductKey,
    string StoreLabel,
    string OfficialStoreExtensionId,
    string ExtensionVersion,
    string DigestFormat,
    string PayloadSha256,
    string ApprovalNote)
{
    public bool Matches(
        string? officialStoreExtensionId,
        string extensionVersion,
        XAdBlockPayloadIdentity payload) =>
        string.Equals(OfficialStoreExtensionId, officialStoreExtensionId, StringComparison.Ordinal)
        && string.Equals(ExtensionVersion, extensionVersion, StringComparison.Ordinal)
        && string.Equals(DigestFormat, payload.DigestFormat, StringComparison.Ordinal)
        && string.Equals(PayloadSha256, payload.Sha256, StringComparison.Ordinal);
}

public static class OfficialAdBlockTrustRecords
{
    public const int CurrentTrustSchemaVersion = 1;
    public const string ProductKey = "adblock-by-eyeo";
    public const string ChromeWebStore = "chrome-web-store";

    public static OfficialAdBlockTrustRecord ChromeWebStore6470 { get; } = new(
        CurrentTrustSchemaVersion,
        ProductKey,
        ChromeWebStore,
        XAdBlockExtensionPolicy.ChromeExtensionId,
        "6.47.0",
        XAdBlockPayloadDigest.DigestFormat,
        "9022cbf484913e8c45cbeae3828c7072de51ffa1369013e7e0aa105172a822ae",
        "Initial reviewed browser-store enrollment authority.");

    public static IReadOnlyList<OfficialAdBlockTrustRecord> All { get; } =
        Array.AsReadOnly([ChromeWebStore6470]);

    public static OfficialAdBlockTrustRecord? FindMatch(
        string? officialStoreExtensionId,
        string extensionVersion,
        XAdBlockPayloadIdentity payload) =>
        All.SingleOrDefault(record => record.Matches(officialStoreExtensionId, extensionVersion, payload));

    public static bool IsApproved(OfficialAdBlockTrustRecord? record) =>
        record is not null && All.Contains(record);
}
