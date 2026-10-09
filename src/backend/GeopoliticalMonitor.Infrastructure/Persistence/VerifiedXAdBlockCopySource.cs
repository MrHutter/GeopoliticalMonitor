using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

/// <summary>
/// The "Use verified X copy" install source for the Bluesky ad blocker. Read-only: it authenticates the X managed copy
/// again (a full hash on every call, nothing cached) and returns its identity as the source of a fresh Bluesky staging
/// copy. It never writes, moves or deletes anything in the X folder and never reads or changes the X binding.
/// </summary>
public sealed class VerifiedXAdBlockCopySource
{
    private readonly string xManagedSourceDirectory;
    private readonly IXDiagnosticAdBlockSourceInspector sourceInspector;
    private readonly IReadOnlyList<OfficialAdBlockTrustRecord> trustRecords;

    public VerifiedXAdBlockCopySource(string xManagedSourceDirectory, IXDiagnosticAdBlockSourceInspector sourceInspector)
        : this(xManagedSourceDirectory, sourceInspector, OfficialAdBlockTrustRecords.All)
    {
    }

    public VerifiedXAdBlockCopySource(
        string xManagedSourceDirectory,
        IXDiagnosticAdBlockSourceInspector sourceInspector,
        IReadOnlyList<OfficialAdBlockTrustRecord> trustRecords)
    {
        this.xManagedSourceDirectory = xManagedSourceDirectory;
        this.sourceInspector = sourceInspector;
        this.trustRecords = trustRecords.ToArray();
    }

    public bool Exists => Directory.Exists(xManagedSourceDirectory);

    public async Task<XDiagnosticAdBlockSourceValidation> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        if (!Exists)
        {
            return XDiagnosticAdBlockSourceValidation.Invalid(
                "No verified X copy exists. Install AdBlock for X first, or choose the reviewed folder with Install AdBlock....");
        }

        var validation = await sourceInspector.InspectAsync(xManagedSourceDirectory, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid || validation.Identity is not { } identity || !IsTrusted(identity))
        {
            return XDiagnosticAdBlockSourceValidation.Invalid(
                "The X copy did not verify against the reviewed trust record; nothing was copied.");
        }

        return validation;
    }

    private bool IsTrusted(XDiagnosticAdBlockSourceIdentity source) =>
        source.Payload is not null
        && source.TrustRecord is not null
        && trustRecords.Contains(source.TrustRecord)
        && source.TrustRecord.Matches(source.Manifest.OfficialStoreExtensionId, source.Manifest.Version, source.Payload);
}
