using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class FileXDiagnosticAdBlockSourceInspector : IXDiagnosticAdBlockSourceInspector
{
    private readonly IReadOnlyList<OfficialAdBlockTrustRecord> trustRecords;

    public FileXDiagnosticAdBlockSourceInspector()
        : this(OfficialAdBlockTrustRecords.All)
    {
    }

    public FileXDiagnosticAdBlockSourceInspector(IReadOnlyList<OfficialAdBlockTrustRecord> trustRecords)
    {
        this.trustRecords = trustRecords;
    }

    public async Task<XDiagnosticAdBlockSourceValidation> InspectAsync(
        string sourceFolderPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFolderPath))
        {
            return Invalid();
        }

        try
        {
            var canonicalPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceFolderPath));
            var payload = await FileXAdBlockPayloadTree.InspectAsync(canonicalPath, cancellationToken).ConfigureAwait(false);
            var filesByPath = payload.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
            if (!filesByPath.TryGetValue("manifest.json", out var manifestFile)
                || !filesByPath.TryGetValue("_locales/en_US/messages.json", out var messagesFile))
            {
                return Invalid();
            }

            var manifestBytes = await FileXAdBlockPayloadTree.ReadBoundedFileAsync(
                manifestFile,
                XAdBlockPayloadLimits.MaximumManifestOrLocaleBytes,
                cancellationToken).ConfigureAwait(false);
            var messageBytes = await FileXAdBlockPayloadTree.ReadBoundedFileAsync(
                messagesFile,
                XAdBlockPayloadLimits.MaximumManifestOrLocaleBytes,
                cancellationToken).ConfigureAwait(false);
            var manifestValidation = XDiagnosticAdBlockManifestPolicy.Inspect(canonicalPath, manifestBytes, messageBytes);
            if (!manifestValidation.IsValid || manifestValidation.Identity is null)
            {
                return manifestValidation;
            }

            var manifest = manifestValidation.Identity.Manifest;
            var trustRecord = trustRecords.SingleOrDefault(record =>
                record.Matches(manifest.OfficialStoreExtensionId, manifest.Version, payload.Identity));
            return XDiagnosticAdBlockSourceValidation.Valid(
                new XDiagnosticAdBlockSourceIdentity(canonicalPath, manifest, payload.Identity, trustRecord));
        }
        catch (IOException)
        {
            return Invalid();
        }
        catch (UnauthorizedAccessException)
        {
            return Invalid();
        }
        catch (InvalidDataException exception)
        {
            return XDiagnosticAdBlockSourceValidation.Invalid(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return XDiagnosticAdBlockSourceValidation.Invalid(exception.Message);
        }
    }

    private static XDiagnosticAdBlockSourceValidation Invalid() =>
        XDiagnosticAdBlockSourceValidation.Invalid(
            "Select the official unpacked AdBlock Chrome MV3 root that directly contains manifest.json.");
}
