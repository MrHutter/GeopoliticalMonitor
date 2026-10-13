using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GeopoliticalMonitor.Application.XBrowser;

public enum XDiagnosticAdBlockState
{
    NotTested,
    NoManagedInstance,
    InstalledManagedEnabled,
    InstalledManagedDisabled,
    InstalledUnmanaged,
    BindingStale,
    InvalidFolder,
    InstallationFailed,
    OperationFailed,
}

public sealed record XDiagnosticAdBlockManifestIdentity(
    int ManifestVersion,
    string Version,
    string RawName,
    string DefaultLocale,
    string DefaultLocaleName,
    string Author,
    bool HasKey,
    string ManifestSha256,
    string ExpectedRuntimeId)
{
    public string? OfficialStoreExtensionId { get; init; }

    public bool HasSamePayload(XDiagnosticAdBlockManifestIdentity other) =>
        ManifestVersion == other.ManifestVersion
        && string.Equals(Version, other.Version, StringComparison.Ordinal)
        && string.Equals(RawName, other.RawName, StringComparison.Ordinal)
        && string.Equals(DefaultLocale, other.DefaultLocale, StringComparison.Ordinal)
        && string.Equals(DefaultLocaleName, other.DefaultLocaleName, StringComparison.Ordinal)
        && string.Equals(Author, other.Author, StringComparison.Ordinal)
        && HasKey == other.HasKey
        && string.Equals(ManifestSha256, other.ManifestSha256, StringComparison.Ordinal);
}

public sealed record XDiagnosticAdBlockSourceIdentity(
    string SourceFolderPath,
    XDiagnosticAdBlockManifestIdentity Manifest,
    XAdBlockPayloadIdentity? Payload = null,
    OfficialAdBlockTrustRecord? TrustRecord = null)
{
    public bool HasTrustedPayload =>
        Payload is not null
        && OfficialAdBlockTrustRecords.IsApproved(TrustRecord)
        && TrustRecord!.Matches(Manifest.OfficialStoreExtensionId, Manifest.Version, Payload);
}

public sealed record XDiagnosticAdBlockSourceValidation(
    bool IsValid,
    XDiagnosticAdBlockSourceIdentity? Identity,
    string? ErrorMessage)
{
    public static XDiagnosticAdBlockSourceValidation Invalid(string message) => new(false, null, message);

    public static XDiagnosticAdBlockSourceValidation Valid(XDiagnosticAdBlockSourceIdentity identity) =>
        new(true, identity, null);
}

public sealed record XDiagnosticAdBlockBinding(
    int SchemaVersion,
    string ProfileScope,
    string RuntimeExtensionId,
    string SourceFolderPath,
    XDiagnosticAdBlockManifestIdentity Manifest,
    XAdBlockPayloadIdentity? Payload = null,
    OfficialAdBlockTrustRecord? TrustRecord = null)
{
    public const int CurrentSchemaVersion = 2;
    public const string DiagnosticProfileScope = "ExtensionCapabilityDiagnostic";
    public const string ProductionXProfileScope = "XBrowser";
    public const string ProductionBlueskyProfileScope = "BlueskyBrowser";

    /// <summary>The production scopes: X and the separate verified Bluesky copy. A binding only ever matches its own scope.</summary>
    public static bool IsProductionScope(string? profileScope) =>
        string.Equals(profileScope, ProductionXProfileScope, StringComparison.Ordinal)
        || string.Equals(profileScope, ProductionBlueskyProfileScope, StringComparison.Ordinal);

    public static XDiagnosticAdBlockBinding Create(
        string runtimeExtensionId,
        XDiagnosticAdBlockSourceIdentity source,
        string profileScope = DiagnosticProfileScope) => new(
        CurrentSchemaVersion,
        profileScope,
        runtimeExtensionId,
        source.SourceFolderPath,
        source.Manifest,
        source.Payload,
        source.TrustRecord);

    public bool Matches(XDiagnosticAdBlockSourceIdentity source, string expectedProfileScope = DiagnosticProfileScope)
    {
        var baseMatch = SchemaVersion == CurrentSchemaVersion
            && string.Equals(ProfileScope, expectedProfileScope, StringComparison.Ordinal)
            && string.Equals(RuntimeExtensionId, source.Manifest.ExpectedRuntimeId, StringComparison.Ordinal)
            && string.Equals(SourceFolderPath, source.SourceFolderPath, StringComparison.OrdinalIgnoreCase)
            && Manifest == source.Manifest
            && Payload == source.Payload
            && TrustRecord == source.TrustRecord;
        if (!baseMatch)
        {
            return false;
        }

        return !IsProductionScope(expectedProfileScope)
            || (source.HasTrustedPayload && OfficialAdBlockTrustRecords.IsApproved(TrustRecord));
    }

    public bool IsStructurallyValid()
    {
        if (SchemaVersion != CurrentSchemaVersion
            || string.IsNullOrWhiteSpace(ProfileScope)
            || string.IsNullOrWhiteSpace(RuntimeExtensionId)
            || string.IsNullOrWhiteSpace(SourceFolderPath)
            || Manifest is null)
        {
            return false;
        }

        if (!IsProductionScope(ProfileScope))
        {
            return string.Equals(ProfileScope, DiagnosticProfileScope, StringComparison.Ordinal);
        }

        return Payload is not null
            && Payload.Sha256.Length == 64
            && Payload.Sha256.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
            && string.Equals(Payload.DigestFormat, XAdBlockPayloadDigest.DigestFormat, StringComparison.Ordinal)
            && OfficialAdBlockTrustRecords.IsApproved(TrustRecord)
            && TrustRecord!.Matches(Manifest.OfficialStoreExtensionId, Manifest.Version, Payload);
    }
}

public interface IXDiagnosticAdBlockBindingStore
{
    Task<XDiagnosticAdBlockBinding?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(XDiagnosticAdBlockBinding binding, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface IXDiagnosticAdBlockSourceInspector
{
    Task<XDiagnosticAdBlockSourceValidation> InspectAsync(
        string sourceFolderPath,
        CancellationToken cancellationToken = default);
}

public sealed record XDiagnosticAdBlockStatus(
    XDiagnosticAdBlockState State,
    XBrowserExtensionInfo? Extension,
    XDiagnosticAdBlockManifestIdentity? Manifest,
    string? Stage,
    string? ExceptionType,
    string? HResult,
    string? Message,
    OfficialAdBlockTrustRecord? TrustRecord = null)
{
    public bool IsManagedInstance =>
        State is XDiagnosticAdBlockState.InstalledManagedEnabled or
            XDiagnosticAdBlockState.InstalledManagedDisabled;

    /// <summary>
    /// Set only by snapshot creation when the binding is valid for the service's scope and the managed payload
    /// authenticated against the compiled trust record, but the bound runtime instance is no longer installed.
    /// The state stays <see cref="XDiagnosticAdBlockState.BindingStale"/>, so every existing rule is unchanged.
    /// </summary>
    public bool BoundInstanceMissing { get; init; }

    public static XDiagnosticAdBlockStatus NotTested { get; } = new(
        XDiagnosticAdBlockState.NotTested,
        Extension: null,
        Manifest: null,
        Stage: null,
        ExceptionType: null,
        HResult: null,
        Message: null);

    public static XDiagnosticAdBlockStatus NoManagedInstance(string? message = null) => new(
        XDiagnosticAdBlockState.NoManagedInstance,
        Extension: null,
        Manifest: null,
        Stage: "Profile.GetBrowserExtensionsAsync",
        ExceptionType: null,
        HResult: null,
        Message: message ?? "No diagnostic AdBlock instance is currently bound for management.");

    public static XDiagnosticAdBlockStatus Managed(
        XBrowserExtensionInfo extension,
        XDiagnosticAdBlockManifestIdentity manifest,
        string? message = null,
        OfficialAdBlockTrustRecord? trustRecord = null) => new(
        extension.IsEnabled
            ? XDiagnosticAdBlockState.InstalledManagedEnabled
            : XDiagnosticAdBlockState.InstalledManagedDisabled,
        extension,
        manifest,
        "Profile.GetBrowserExtensionsAsync",
        ExceptionType: null,
        HResult: null,
        Message: message ?? "The diagnostic managed-instance binding is valid.",
        trustRecord);

    public static XDiagnosticAdBlockStatus InstalledUnmanaged(
        XBrowserExtensionInfo extension,
        XDiagnosticAdBlockManifestIdentity? manifest,
        string message,
        OfficialAdBlockTrustRecord? trustRecord = null) => new(
        XDiagnosticAdBlockState.InstalledUnmanaged,
        extension,
        manifest,
        "Profile.GetBrowserExtensionsAsync",
        ExceptionType: null,
        HResult: null,
        message,
        trustRecord);

    public static XDiagnosticAdBlockStatus BindingStale(
        string message,
        XBrowserExtensionInfo? extension = null) => new(
        XDiagnosticAdBlockState.BindingStale,
        extension,
        Manifest: null,
        "Diagnostic binding validation",
        ExceptionType: null,
        HResult: null,
        message);

    public static XDiagnosticAdBlockStatus InvalidFolder(string message) => new(
        XDiagnosticAdBlockState.InvalidFolder,
        Extension: null,
        Manifest: null,
        "Folder validation",
        ExceptionType: null,
        HResult: null,
        message);

    public static XDiagnosticAdBlockStatus Failed(
        XDiagnosticAdBlockState state,
        string stage,
        Exception exception,
        string safeMessage) => new(
        state,
        Extension: null,
        Manifest: null,
        stage,
        exception.GetType().FullName,
        $"0x{exception.HResult:X8}",
        safeMessage);

    public static XDiagnosticAdBlockStatus OperationFailed(string stage, string message) => new(
        XDiagnosticAdBlockState.OperationFailed,
        Extension: null,
        Manifest: null,
        stage,
        ExceptionType: null,
        HResult: null,
        message);
}

public sealed record XDiagnosticAdBlockSnapshot(
    IReadOnlyList<XBrowserExtensionInfo> Extensions,
    XDiagnosticAdBlockStatus AdBlockStatus);

public static class ChromiumExtensionIdentity
{
    public static string GenerateFromUnpackedPath(string sourceFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFolderPath);
        var normalized = NormalizeWindowsDriveLetter(sourceFolderPath);
        return Generate(Encoding.Unicode.GetBytes(normalized));
    }

    public static string GenerateFromManifestKey(string manifestKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestKey);
        return Generate(Convert.FromBase64String(manifestKey));
    }

    private static string Generate(ReadOnlySpan<byte> input)
    {
        var hash = SHA256.HashData(input);
        var id = new char[32];
        for (var index = 0; index < 16; index++)
        {
            id[index * 2] = (char)('a' + (hash[index] >> 4));
            id[(index * 2) + 1] = (char)('a' + (hash[index] & 0x0F));
        }

        return new string(id);
    }

    private static string NormalizeWindowsDriveLetter(string path)
    {
        if (path.Length >= 2 && path[1] == ':' && path[0] is >= 'a' and <= 'z')
        {
            return char.ToUpperInvariant(path[0]) + path[1..];
        }

        return path;
    }
}

public static class XDiagnosticAdBlockManifestPolicy
{
    private const int MaximumManifestBytes = 1024 * 1024;
    private const int MaximumMetadataLength = 200;
    private const string ExpectedRawName = "__MSG_name__";
    private const string ExpectedDefaultLocale = "en_US";
    private const string ExpectedAuthor = "Adblock, Inc.";

    public static XDiagnosticAdBlockSourceValidation Inspect(
        string sourceFolderPath,
        byte[] manifestBytes,
        byte[] defaultLocaleMessagesBytes)
    {
        if (string.IsNullOrWhiteSpace(sourceFolderPath)
            || manifestBytes.Length == 0
            || manifestBytes.Length > MaximumManifestBytes
            || defaultLocaleMessagesBytes.Length == 0
            || defaultLocaleMessagesBytes.Length > MaximumManifestBytes)
        {
            return Invalid();
        }

        try
        {
            using var manifestDocument = JsonDocument.Parse(manifestBytes);
            using var messagesDocument = JsonDocument.Parse(defaultLocaleMessagesBytes);
            var root = manifestDocument.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryGetInt32(root, "manifest_version", out var manifestVersion)
                || manifestVersion != 3
                || !TryGetBoundedString(root, "version", out var version)
                || !TryGetBoundedString(root, "name", out var rawName)
                || !string.Equals(rawName, ExpectedRawName, StringComparison.Ordinal)
                || !TryGetBoundedString(root, "default_locale", out var defaultLocale)
                || !string.Equals(defaultLocale, ExpectedDefaultLocale, StringComparison.Ordinal)
                || !TryGetBoundedString(root, "author", out var author)
                || !string.Equals(author, ExpectedAuthor, StringComparison.Ordinal)
                || !ContainsKnownStoreIds(root)
                || !TryGetLocalizedName(messagesDocument.RootElement, out var localizedName))
            {
                return Invalid();
            }

            string? manifestKey = null;
            if (root.TryGetProperty("key", out var keyElement))
            {
                if (keyElement.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(keyElement.GetString())
                    || keyElement.GetString()!.Length > 4096)
                {
                    return Invalid();
                }

                manifestKey = keyElement.GetString();
            }

            var canonicalPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceFolderPath));
            var expectedRuntimeId = manifestKey is null
                ? ChromiumExtensionIdentity.GenerateFromUnpackedPath(canonicalPath)
                : ChromiumExtensionIdentity.GenerateFromManifestKey(manifestKey);
            var officialStoreExtensionId = manifestKey is null ? null : expectedRuntimeId;
            var manifestHash = Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant();
            var identity = new XDiagnosticAdBlockSourceIdentity(
                canonicalPath,
                new XDiagnosticAdBlockManifestIdentity(
                    manifestVersion,
                    version,
                    rawName,
                    defaultLocale,
                    localizedName,
                    author,
                    HasKey: manifestKey is not null,
                    manifestHash,
                    expectedRuntimeId)
                {
                    OfficialStoreExtensionId = officialStoreExtensionId,
                });
            return XDiagnosticAdBlockSourceValidation.Valid(identity);
        }
        catch (JsonException)
        {
            return Invalid();
        }
        catch (FormatException)
        {
            return Invalid();
        }
    }

    private static bool ContainsKnownStoreIds(JsonElement root)
    {
        if (!root.TryGetProperty("externally_connectable", out var externallyConnectable)
            || externallyConnectable.ValueKind != JsonValueKind.Object
            || !externallyConnectable.TryGetProperty("ids", out var ids)
            || ids.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var values = ids.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => item is not null)
            .ToHashSet(StringComparer.Ordinal);
        return values.Contains(XAdBlockExtensionPolicy.ChromeExtensionId)
            && values.Contains(XAdBlockExtensionPolicy.EdgeExtensionId);
    }

    private static bool TryGetLocalizedName(JsonElement messagesRoot, out string value)
    {
        value = string.Empty;
        return messagesRoot.ValueKind == JsonValueKind.Object
            && messagesRoot.TryGetProperty("name", out var name)
            && name.ValueKind == JsonValueKind.Object
            && TryGetBoundedString(name, "message", out value)
            && value.StartsWith("AdBlock", StringComparison.Ordinal);
    }

    private static bool TryGetInt32(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        return root.TryGetProperty(propertyName, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value);
    }

    private static bool TryGetBoundedString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var candidate = element.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaximumMetadataLength)
        {
            return false;
        }

        value = candidate;
        return true;
    }

    private static XDiagnosticAdBlockSourceValidation Invalid() =>
        XDiagnosticAdBlockSourceValidation.Invalid(
            "Select the official unpacked AdBlock Chrome MV3 root with its unchanged manifest and locale metadata.");
}

public sealed class XDiagnosticAdBlockService(
    IXBrowserExtensionProfile profile,
    IXDiagnosticAdBlockBindingStore bindingStore,
    IXDiagnosticAdBlockSourceInspector sourceInspector,
    string profileScope = XDiagnosticAdBlockBinding.DiagnosticProfileScope,
    string? unmanagedCandidateSourcePath = null)
{
    private static readonly HashSet<string> ProtectedMicrosoftExtensionIds = new(StringComparer.Ordinal)
    {
        "dgiklkfkllikcanfonkcabmbdfmgleag",
        "mhjfbmdgcfjbbpaeojofohoefgiehjai",
    };

    private string ProfileDisplayName => profileScope switch
    {
        XDiagnosticAdBlockBinding.ProductionXProfileScope => "production X",
        XDiagnosticAdBlockBinding.ProductionBlueskyProfileScope => "production Bluesky",
        _ => "diagnostic",
    };

    private string BindingActionName =>
        XDiagnosticAdBlockBinding.IsProductionScope(profileScope) ? "Reconcile" : "Bind Installed AdBlock";

    public async Task<XDiagnosticAdBlockSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var extensions = await profile.GetInstalledExtensionsAsync(cancellationToken).ConfigureAwait(true);
        return await CreateSnapshotAsync(extensions, cancellationToken).ConfigureAwait(true);
    }

    public async Task<XDiagnosticAdBlockSnapshot> SecureForAuthenticatedNavigationAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(true);
        if (!XDiagnosticAdBlockBinding.IsProductionScope(profileScope)
            || snapshot.AdBlockStatus.IsManagedInstance)
        {
            return snapshot;
        }

        var untrustedExactRuntimeId = snapshot.AdBlockStatus.IsManagedInstance
            ? null
            : snapshot.AdBlockStatus.Extension?.Id;
        var possibleAdBlockExtensions = snapshot.Extensions
            .Where(extension => !IsProtectedMicrosoftExtensionId(extension.Id))
            .Where(extension => string.Equals(extension.Id, untrustedExactRuntimeId, StringComparison.Ordinal)
                || XAdBlockExtensionPolicy.HasKnownStoreIdentity(extension)
                || extension.Name.StartsWith("AdBlock", StringComparison.OrdinalIgnoreCase))
            .Where(extension => extension.IsEnabled)
            .ToArray();
        foreach (var extension in possibleAdBlockExtensions)
        {
            await profile.SetExtensionEnabledAsync(extension.Id, enabled: false, cancellationToken).ConfigureAwait(true);
        }

        return possibleAdBlockExtensions.Length == 0
            ? snapshot
            : await GetSnapshotAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task<XDiagnosticAdBlockSnapshot> InstallAsync(
        string extensionFolderPath,
        CancellationToken cancellationToken = default)
    {
        var baseline = await profile.GetInstalledExtensionsAsync(cancellationToken).ConfigureAwait(true);
        var current = await CreateSnapshotAsync(baseline, cancellationToken).ConfigureAwait(true);
        if (current.AdBlockStatus.IsManagedInstance)
        {
            return current with
            {
                AdBlockStatus = current.AdBlockStatus with
                {
                    Message = $"The {ProfileDisplayName} managed AdBlock instance is already installed; installation was skipped.",
                },
            };
        }

        var validation = await sourceInspector.InspectAsync(extensionFolderPath, cancellationToken).ConfigureAwait(true);
        if (!validation.IsValid || validation.Identity is null)
        {
            return new(baseline, XDiagnosticAdBlockStatus.InvalidFolder(validation.ErrorMessage ?? "The selected folder is invalid."));
        }

        var source = validation.Identity;
        if (!IsEligibleForProfile(source))
        {
            return new(
                baseline,
                XDiagnosticAdBlockStatus.InvalidFolder(
                    "The selected AdBlock payload does not match a reviewed production trust record."));
        }

        var existing = FindExactExtension(baseline, source.Manifest.ExpectedRuntimeId);
        if (existing is not null)
        {
            return new(
                baseline,
                XDiagnosticAdBlockStatus.InstalledUnmanaged(
                    existing,
                    source.Manifest,
                    $"This source folder is already installed. Use {BindingActionName} to establish {ProfileDisplayName} management without reinstalling it.",
                    source.TrustRecord));
        }

        var baselineIds = baseline.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var added = await profile.AddBrowserExtensionAsync(source.SourceFolderPath, cancellationToken).ConfigureAwait(true);
        var extensions = await profile.GetInstalledExtensionsAsync(cancellationToken).ConfigureAwait(true);
        var addedByDelta = extensions.Where(item => !baselineIds.Contains(item.Id)).ToArray();
        var installed = FindExactExtension(extensions, added.Id);
        if (addedByDelta.Length != 1
            || installed is null
            || !string.Equals(addedByDelta[0].Id, added.Id, StringComparison.Ordinal)
            || !string.Equals(added.Id, source.Manifest.ExpectedRuntimeId, StringComparison.Ordinal)
            || IsProtectedMicrosoftExtensionId(added.Id))
        {
            if (addedByDelta.Any(item => string.Equals(item.Id, added.Id, StringComparison.Ordinal))
                && !IsProtectedMicrosoftExtensionId(added.Id))
            {
                await profile.RemoveExtensionAsync(added.Id, cancellationToken).ConfigureAwait(true);
                extensions = await profile.GetInstalledExtensionsAsync(cancellationToken).ConfigureAwait(true);
            }

            return new(
                extensions,
                XDiagnosticAdBlockStatus.InstalledUnmanaged(
                    added,
                    source.Manifest,
                    "Installation could not be bound safely to the explicit install transaction, so the new instance was removed.",
                    source.TrustRecord));
        }

        try
        {
            await bindingStore.SaveAsync(
                XDiagnosticAdBlockBinding.Create(installed.Id, source, profileScope),
                cancellationToken).ConfigureAwait(true);
        }
        catch
        {
            await profile.RemoveExtensionAsync(installed.Id, cancellationToken).ConfigureAwait(true);
            throw;
        }

        return new(
            extensions,
            XDiagnosticAdBlockStatus.Managed(
                installed,
                source.Manifest,
                $"AdBlock was installed and bound to the {profileScope} profile.",
                source.TrustRecord));
    }

    /// <summary>
    /// User-invoked restore of the bound production X instance after the browser dropped it. Takes no source folder:
    /// it re-checks the binding and authenticates the configured managed payload again, and only when the bound
    /// instance is missing does it add the extension from that managed folder through <see cref="InstallAsync"/>,
    /// which authenticates the payload once more and re-binds to the X scope. Nothing is written into the payload.
    /// </summary>
    public async Task<XDiagnosticAdBlockSnapshot> RestoreBoundInstanceAsync(CancellationToken cancellationToken = default)
    {
        var current = await GetSnapshotAsync(cancellationToken).ConfigureAwait(true);
        if (!string.Equals(profileScope, XDiagnosticAdBlockBinding.ProductionXProfileScope, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(unmanagedCandidateSourcePath)
            || !current.AdBlockStatus.BoundInstanceMissing)
        {
            return RestoreNotPerformed(current);
        }

        var binding = await bindingStore.GetAsync(cancellationToken).ConfigureAwait(true);
        if (binding is null || !IsSameFolder(binding.SourceFolderPath, unmanagedCandidateSourcePath))
        {
            return RestoreNotPerformed(current);
        }

        return await InstallAsync(unmanagedCandidateSourcePath, cancellationToken).ConfigureAwait(true);
    }

    public async Task<XDiagnosticAdBlockSnapshot> BindInstalledAsync(
        string extensionFolderPath,
        CancellationToken cancellationToken = default)
    {
        var extensions = await profile.GetInstalledExtensionsAsync(cancellationToken).ConfigureAwait(true);
        var validation = await sourceInspector.InspectAsync(extensionFolderPath, cancellationToken).ConfigureAwait(true);
        if (!validation.IsValid || validation.Identity is null)
        {
            return new(extensions, XDiagnosticAdBlockStatus.InvalidFolder(validation.ErrorMessage ?? "The selected folder is invalid."));
        }

        var source = validation.Identity;
        if (!IsEligibleForProfile(source))
        {
            return new(
                extensions,
                XDiagnosticAdBlockStatus.InvalidFolder(
                    "The selected AdBlock payload does not match a reviewed production trust record."));
        }

        var extension = FindExactExtension(extensions, source.Manifest.ExpectedRuntimeId);
        if (extension is null || IsProtectedMicrosoftExtensionId(extension.Id))
        {
            return new(
                extensions,
                XDiagnosticAdBlockStatus.OperationFailed(
                    $"{ProfileDisplayName} binding validation",
                    $"The selected source folder does not match an installed extension in the {ProfileDisplayName} profile."));
        }

        if (XDiagnosticAdBlockBinding.IsProductionScope(profileScope))
        {
            await profile.RemoveExtensionAsync(extension.Id, cancellationToken).ConfigureAwait(true);
            var afterRemoval = await profile.GetInstalledExtensionsAsync(cancellationToken).ConfigureAwait(true);
            if (FindExactExtension(afterRemoval, extension.Id) is not null)
            {
                return new(
                    afterRemoval,
                    XDiagnosticAdBlockStatus.OperationFailed(
                        "Production reconciliation",
                        "The existing runtime instance could not be removed before trusted reinstallation."));
            }

            return await InstallAsync(extensionFolderPath, cancellationToken).ConfigureAwait(true);
        }

        await bindingStore.SaveAsync(
            XDiagnosticAdBlockBinding.Create(extension.Id, source, profileScope),
            cancellationToken).ConfigureAwait(true);
        return new(
            extensions,
            XDiagnosticAdBlockStatus.Managed(
                extension,
                source.Manifest,
                $"The installed unpacked instance is now managed only in the {profileScope} profile.",
                source.TrustRecord));
    }

    public async Task<XDiagnosticAdBlockSnapshot> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var current = await GetSnapshotAsync(cancellationToken).ConfigureAwait(true);
        var extension = GetManagedActionTarget(current);
        if (extension is null)
        {
            return current;
        }

        await profile.SetExtensionEnabledAsync(extension.Id, enabled, cancellationToken).ConfigureAwait(true);
        var extensions = await profile.GetInstalledExtensionsAsync(cancellationToken).ConfigureAwait(true);
        var refreshed = FindExactExtension(extensions, extension.Id);
        if (refreshed is null || refreshed.IsEnabled != enabled)
        {
            throw new InvalidOperationException("WebView2 did not report the requested AdBlock enabled state.");
        }

        var binding = await bindingStore.GetAsync(cancellationToken).ConfigureAwait(true)
            ?? throw new InvalidOperationException($"The {ProfileDisplayName} managed-instance binding is unavailable.");
        return new(
            extensions,
            XDiagnosticAdBlockStatus.Managed(
                refreshed,
                binding.Manifest,
                enabled ? $"The {ProfileDisplayName} managed AdBlock instance is enabled." : $"The {ProfileDisplayName} managed AdBlock instance is disabled.",
                binding.TrustRecord));
    }

    public async Task<XDiagnosticAdBlockSnapshot> RemoveAsync(CancellationToken cancellationToken = default)
    {
        var current = await GetSnapshotAsync(cancellationToken).ConfigureAwait(true);
        var extension = GetManagedActionTarget(current);
        if (extension is null)
        {
            return current;
        }

        await profile.RemoveExtensionAsync(extension.Id, cancellationToken).ConfigureAwait(true);
        var extensions = await profile.GetInstalledExtensionsAsync(cancellationToken).ConfigureAwait(true);
        if (FindExactExtension(extensions, extension.Id) is not null)
        {
            throw new InvalidOperationException("WebView2 still reports AdBlock after removal.");
        }

        await bindingStore.ClearAsync(cancellationToken).ConfigureAwait(true);
        return new(
            extensions,
            XDiagnosticAdBlockStatus.NoManagedInstance($"The {ProfileDisplayName} managed AdBlock instance was removed and its binding was cleared."));
    }

    private async Task<XDiagnosticAdBlockSnapshot> CreateSnapshotAsync(
        IReadOnlyList<XBrowserExtensionInfo> extensions,
        CancellationToken cancellationToken)
    {
        var binding = await bindingStore.GetAsync(cancellationToken).ConfigureAwait(true);
        if (binding is null)
        {
            if (!string.IsNullOrWhiteSpace(unmanagedCandidateSourcePath))
            {
                var candidate = await sourceInspector.InspectAsync(unmanagedCandidateSourcePath, cancellationToken).ConfigureAwait(true);
                if (candidate.IsValid && candidate.Identity is not null && IsEligibleForProfile(candidate.Identity))
                {
                    var exactCandidate = FindExactExtension(extensions, candidate.Identity.Manifest.ExpectedRuntimeId);
                    if (exactCandidate is not null && !IsProtectedMicrosoftExtensionId(exactCandidate.Id))
                    {
                        return new(
                            extensions,
                            XDiagnosticAdBlockStatus.InstalledUnmanaged(
                                exactCandidate,
                                candidate.Identity.Manifest,
                                $"The application-managed payload is installed in the {ProfileDisplayName} profile but is not bound. Use {BindingActionName}.",
                                candidate.Identity.TrustRecord));
                    }
                }
            }

            var knownStoreExtensions = extensions.Where(XAdBlockExtensionPolicy.HasKnownStoreIdentity).ToArray();
            return knownStoreExtensions.Length == 1
                ? new(
                    extensions,
                    XDiagnosticAdBlockStatus.InstalledUnmanaged(
                        knownStoreExtensions[0],
                        manifest: null,
                        $"A known Store identity is present, but no owner-managed {ProfileDisplayName} binding exists."))
                : new(extensions, XDiagnosticAdBlockStatus.NoManagedInstance($"No {ProfileDisplayName} AdBlock instance is currently bound for management."));
        }

        if (binding.SchemaVersion != XDiagnosticAdBlockBinding.CurrentSchemaVersion
            || !string.Equals(binding.ProfileScope, profileScope, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(binding.RuntimeExtensionId)
            || (XDiagnosticAdBlockBinding.IsProductionScope(profileScope)
                && (!OfficialAdBlockTrustRecords.IsApproved(binding.TrustRecord) || binding.Payload is null))
            || IsProtectedMicrosoftExtensionId(binding.RuntimeExtensionId))
        {
            return new(extensions, XDiagnosticAdBlockStatus.BindingStale($"The saved {ProfileDisplayName} binding is invalid; management is disabled."));
        }

        var validation = await sourceInspector.InspectAsync(binding.SourceFolderPath, cancellationToken).ConfigureAwait(true);
        if (!validation.IsValid || validation.Identity is null || !binding.Matches(validation.Identity, profileScope))
        {
            return new(
                extensions,
                XDiagnosticAdBlockStatus.BindingStale(
                    $"The saved source folder or manifest no longer matches the {ProfileDisplayName} binding; management is disabled.",
                    FindExactExtension(extensions, binding.RuntimeExtensionId)));
        }

        var extension = FindExactExtension(extensions, binding.RuntimeExtensionId);
        if (extension is null)
        {
            return new(
                extensions,
                XDiagnosticAdBlockStatus.BindingStale($"The bound {ProfileDisplayName} AdBlock instance is no longer installed; management is disabled.") with
                {
                    BoundInstanceMissing = true,
                });
        }

        return new(
            extensions,
            XDiagnosticAdBlockStatus.Managed(
                extension,
                binding.Manifest,
                $"The {ProfileDisplayName} managed-instance binding is valid.",
                binding.TrustRecord));
    }

    private static XDiagnosticAdBlockSnapshot RestoreNotPerformed(XDiagnosticAdBlockSnapshot current) => current with
    {
        AdBlockStatus = current.AdBlockStatus with
        {
            Message = $"{XProductionAdBlockPolicy.RestoreNotPerformedMessage} {current.AdBlockStatus.Message}".Trim(),
        },
    };

    private static bool IsSameFolder(string first, string second) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            StringComparison.OrdinalIgnoreCase);

    private static XBrowserExtensionInfo? GetManagedActionTarget(XDiagnosticAdBlockSnapshot snapshot)
    {
        var extension = snapshot.AdBlockStatus.IsManagedInstance ? snapshot.AdBlockStatus.Extension : null;
        return extension is not null && !IsProtectedMicrosoftExtensionId(extension.Id) ? extension : null;
    }

    private static XBrowserExtensionInfo? FindExactExtension(
        IReadOnlyList<XBrowserExtensionInfo> extensions,
        string extensionId)
    {
        var matches = extensions.Where(item => string.Equals(item.Id, extensionId, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool IsProtectedMicrosoftExtensionId(string? extensionId) =>
        extensionId is not null && ProtectedMicrosoftExtensionIds.Contains(extensionId);

    private bool IsEligibleForProfile(XDiagnosticAdBlockSourceIdentity source) =>
        !XDiagnosticAdBlockBinding.IsProductionScope(profileScope)
        || source.HasTrustedPayload;
}
