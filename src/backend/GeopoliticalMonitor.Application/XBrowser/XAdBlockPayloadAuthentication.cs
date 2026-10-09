using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace GeopoliticalMonitor.Application.XBrowser;

public static class XAdBlockPayloadLimits
{
    public const int MaximumRuntimeFiles = 1024;
    public const long MaximumAggregateRuntimeBytes = 512L * 1024 * 1024;
    public const long MaximumIndividualFileBytes = 32L * 1024 * 1024;
    public const int MaximumDirectories = 256;
    public const int MaximumDirectoryDepth = 16;
    public const int MaximumNormalizedPathUtf8Bytes = 512;
    public const int MaximumManifestOrLocaleBytes = 1024 * 1024;
}

public sealed record XAdBlockPayloadIdentity(
    string DigestFormat,
    string Sha256,
    int FileCount,
    long TotalBytes,
    int DirectoryCount,
    int MaximumDirectoryDepth,
    int LongestPathUtf8Bytes);

public sealed record XAdBlockPayloadFileHash(
    string RelativePath,
    long Length,
    byte[] Sha256);

public sealed class XAdBlockPayloadResourceBudget
{
    public int FileCount { get; private set; }

    public long TotalBytes { get; private set; }

    public int DirectoryCount { get; private set; }

    public int MaximumDirectoryDepth { get; private set; }

    public int LongestPathUtf8Bytes { get; private set; }

    public void AddDirectory(int depth)
    {
        if (depth < 1 || depth > XAdBlockPayloadLimits.MaximumDirectoryDepth)
        {
            throw new InvalidDataException("The extension payload exceeds the directory-depth limit.");
        }

        DirectoryCount++;
        if (DirectoryCount > XAdBlockPayloadLimits.MaximumDirectories)
        {
            throw new InvalidDataException("The extension payload exceeds the directory-count limit.");
        }

        MaximumDirectoryDepth = Math.Max(MaximumDirectoryDepth, depth);
    }

    public void AddFile(string normalizedRelativePath, long length)
    {
        var path = XAdBlockPayloadDigest.NormalizeRelativePath(normalizedRelativePath);
        var depth = path.Count(character => character == '/');
        if (depth > XAdBlockPayloadLimits.MaximumDirectoryDepth)
        {
            throw new InvalidDataException("The extension payload exceeds the directory-depth limit.");
        }

        if (length < 0 || length > XAdBlockPayloadLimits.MaximumIndividualFileBytes)
        {
            throw new InvalidDataException("The extension payload contains a file larger than the configured limit.");
        }

        FileCount++;
        if (FileCount > XAdBlockPayloadLimits.MaximumRuntimeFiles)
        {
            throw new InvalidDataException("The extension payload exceeds the file-count limit.");
        }

        TotalBytes = checked(TotalBytes + length);
        if (TotalBytes > XAdBlockPayloadLimits.MaximumAggregateRuntimeBytes)
        {
            throw new InvalidDataException("The extension payload exceeds the aggregate byte limit.");
        }

        MaximumDirectoryDepth = Math.Max(MaximumDirectoryDepth, depth);
        LongestPathUtf8Bytes = Math.Max(LongestPathUtf8Bytes, XAdBlockPayloadDigest.StrictUtf8.GetByteCount(path));
    }
}

public static class XAdBlockPayloadDigest
{
    public const string DigestFormat = "gpm-extension-payload-v1";

    internal static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static string NormalizeRelativePath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        if (relativePath.Length == 0 || relativePath.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("Extension payload paths must be non-empty relative paths.", nameof(relativePath));
        }

        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.StartsWith("//", StringComparison.Ordinal)
            || (normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':'))
        {
            throw new ArgumentException("Extension payload paths must not be rooted.", nameof(relativePath));
        }

        var components = normalized.Split('/');
        if (components.Any(component => component.Length == 0 || component is "." or ".."))
        {
            throw new ArgumentException("Extension payload paths contain an invalid component.", nameof(relativePath));
        }

        int encodedLength;
        try
        {
            encodedLength = StrictUtf8.GetByteCount(normalized);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException("Extension payload paths must contain valid UTF-16.", nameof(relativePath), exception);
        }

        if (encodedLength > XAdBlockPayloadLimits.MaximumNormalizedPathUtf8Bytes)
        {
            throw new ArgumentException("Extension payload paths exceed the configured limit.", nameof(relativePath));
        }

        return normalized;
    }

    public static string Compute(IEnumerable<XAdBlockPayloadFileHash> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var records = files
            .Select(file => new XAdBlockPayloadFileHash(
                NormalizeRelativePath(file.RelativePath),
                file.Length,
                file.Sha256.ToArray()))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();

        if (records.Length > XAdBlockPayloadLimits.MaximumRuntimeFiles)
        {
            throw new InvalidDataException("The extension payload exceeds the file-count limit.");
        }

        var paths = new HashSet<string>(StringComparer.Ordinal);
        using var outerHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        outerHash.AppendData(Encoding.ASCII.GetBytes("GPM-EXTENSION-PAYLOAD"));
        outerHash.AppendData([0]);
        AppendUInt32(outerHash, 1);
        AppendUInt64(outerHash, checked((ulong)records.LongLength));

        foreach (var record in records)
        {
            if (!paths.Add(record.RelativePath))
            {
                throw new InvalidDataException("The extension payload contains duplicate normalized paths.");
            }

            if (record.Length < 0 || record.Sha256.Length != SHA256.HashSizeInBytes)
            {
                throw new InvalidDataException("The extension payload contains an invalid file record.");
            }

            var pathBytes = StrictUtf8.GetBytes(record.RelativePath);
            outerHash.AppendData([1]);
            AppendUInt32(outerHash, checked((uint)pathBytes.Length));
            outerHash.AppendData(pathBytes);
            AppendUInt64(outerHash, checked((ulong)record.Length));
            outerHash.AppendData(record.Sha256);
        }

        return Convert.ToHexString(outerHash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendUInt32(IncrementalHash hash, uint value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static void AppendUInt64(IncrementalHash hash, ulong value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }
}

public sealed record XProductionAdBlockStartupDecision(
    bool AttemptExtensionEnvironment,
    bool HasTrustedManagedPayload,
    string? Message);

public static class XProductionAdBlockStartupTrustPolicy
{
    /// <param name="profileScope">
    /// The production scope whose own payload and binding are evaluated: X (the default, unchanged) or the separate
    /// verified Bluesky copy. A clean state allows the extension environment for X only; Bluesky stays
    /// extension-disabled until the owner installs the reviewed ad blocker for it.
    /// </param>
    public static XProductionAdBlockStartupDecision Evaluate(
        bool managedPayloadExists,
        bool persistedBindingExists,
        XDiagnosticAdBlockBinding? binding,
        XDiagnosticAdBlockSourceValidation? managedPayloadValidation,
        string profileScope = XDiagnosticAdBlockBinding.ProductionXProfileScope)
    {
        if (!XDiagnosticAdBlockBinding.IsProductionScope(profileScope))
        {
            throw new ArgumentOutOfRangeException(nameof(profileScope), "Only a production profile scope has a startup trust gate.");
        }

        if (!managedPayloadExists && !persistedBindingExists)
        {
            return new(
                AttemptExtensionEnvironment: string.Equals(profileScope, XDiagnosticAdBlockBinding.ProductionXProfileScope, StringComparison.Ordinal),
                HasTrustedManagedPayload: false,
                Message: null);
        }

        if (managedPayloadExists
            && !persistedBindingExists
            && binding is null
            && managedPayloadValidation?.IsValid == true
            && managedPayloadValidation.Identity?.HasTrustedPayload == true)
        {
            return new(
                AttemptExtensionEnvironment: true,
                HasTrustedManagedPayload: true,
                "The reviewed managed payload is ready for a fresh WebView2 install transaction.");
        }

        if (managedPayloadExists
            && binding is not null
            && managedPayloadValidation?.IsValid == true
            && managedPayloadValidation.Identity is not null
            && binding.Matches(managedPayloadValidation.Identity, profileScope))
        {
            return new(true, true, null);
        }

        return new(
            AttemptExtensionEnvironment: false,
            HasTrustedManagedPayload: false,
            "Saved AdBlock state is not trusted under the current reviewed authority. Re-enroll the approved payload and restart the application.");
    }
}
