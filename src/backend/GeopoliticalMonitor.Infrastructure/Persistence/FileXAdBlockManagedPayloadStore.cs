using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed record XAdBlockManagedPayloadPreparation(
    bool Success,
    XDiagnosticAdBlockSourceIdentity? Identity,
    bool ReusedExisting,
    string? ErrorMessage);

public sealed record XAdBlockManagedPayloadCleanup(bool Success, bool Removed, string? ErrorMessage);

public interface IXAdBlockManagedPayloadDirectoryOperations
{
    bool Exists(string path);

    void Move(string source, string destination);

    void Delete(string path, bool recursive);
}

public interface IXAdBlockManagedPayloadFileCopier
{
    Task CopyAsync(
        string sourcePath,
        long expectedLength,
        byte[] expectedSha256,
        string destinationPath,
        CancellationToken cancellationToken);
}

public sealed class SystemXAdBlockManagedPayloadFileCopier : IXAdBlockManagedPayloadFileCopier
{
    public Task CopyAsync(
        string sourcePath,
        long expectedLength,
        byte[] expectedSha256,
        string destinationPath,
        CancellationToken cancellationToken) =>
        FileXAdBlockPayloadTree.CopyFileAsync(
            new XAdBlockRuntimeFile(
                RelativePath: Path.GetFileName(destinationPath),
                sourcePath,
                expectedLength,
                LastWriteTimeUtc: default,
                expectedSha256),
            destinationPath,
            cancellationToken);
}

public sealed class SystemXAdBlockManagedPayloadDirectoryOperations : IXAdBlockManagedPayloadDirectoryOperations
{
    public bool Exists(string path) => Directory.Exists(path);

    public void Move(string source, string destination) => Directory.Move(source, destination);

    public void Delete(string path, bool recursive) => Directory.Delete(path, recursive);
}

public sealed class FileXAdBlockManagedPayloadStore
{
    private readonly string managedSourceDirectory;
    private readonly IXDiagnosticAdBlockSourceInspector sourceInspector;
    private readonly IXAdBlockManagedPayloadDirectoryOperations directoryOperations;
    private readonly IXAdBlockManagedPayloadFileCopier fileCopier;
    private readonly IReadOnlyList<OfficialAdBlockTrustRecord> trustRecords;

    public FileXAdBlockManagedPayloadStore(
        string managedSourceDirectory,
        IXDiagnosticAdBlockSourceInspector sourceInspector)
        : this(
            managedSourceDirectory,
            sourceInspector,
            new SystemXAdBlockManagedPayloadDirectoryOperations(),
            new SystemXAdBlockManagedPayloadFileCopier(),
            OfficialAdBlockTrustRecords.All)
    {
    }

    public FileXAdBlockManagedPayloadStore(
        string managedSourceDirectory,
        IXDiagnosticAdBlockSourceInspector sourceInspector,
        IXAdBlockManagedPayloadDirectoryOperations directoryOperations)
        : this(
            managedSourceDirectory,
            sourceInspector,
            directoryOperations,
            new SystemXAdBlockManagedPayloadFileCopier(),
            OfficialAdBlockTrustRecords.All)
    {
    }

    public FileXAdBlockManagedPayloadStore(
        string managedSourceDirectory,
        IXDiagnosticAdBlockSourceInspector sourceInspector,
        IXAdBlockManagedPayloadDirectoryOperations directoryOperations,
        IReadOnlyList<OfficialAdBlockTrustRecord> trustRecords)
        : this(
            managedSourceDirectory,
            sourceInspector,
            directoryOperations,
            new SystemXAdBlockManagedPayloadFileCopier(),
            trustRecords)
    {
    }

    public FileXAdBlockManagedPayloadStore(
        string managedSourceDirectory,
        IXDiagnosticAdBlockSourceInspector sourceInspector,
        IXAdBlockManagedPayloadDirectoryOperations directoryOperations,
        IXAdBlockManagedPayloadFileCopier fileCopier,
        IReadOnlyList<OfficialAdBlockTrustRecord> trustRecords)
    {
        this.managedSourceDirectory = Canonicalize(managedSourceDirectory);
        this.sourceInspector = sourceInspector;
        this.directoryOperations = directoryOperations;
        this.fileCopier = fileCopier;
        this.trustRecords = trustRecords.ToArray();
    }

    public async Task<XAdBlockManagedPayloadPreparation> PrepareAsync(
        XDiagnosticAdBlockSourceIdentity selectedSource,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedSource);
        if (!IsTrustedByStore(selectedSource))
        {
            return Failed("The selected AdBlock payload does not match a reviewed production trust record.");
        }

        var stagingPath = $"{managedSourceDirectory}.staging";
        var backupPath = $"{managedSourceDirectory}.previous";
        var rejectedPath = $"{managedSourceDirectory}.rejected";
        var movedExistingToBackup = false;
        var activatedStaging = false;

        try
        {
            if (!await RecoverInterruptedActivationAsync(cancellationToken).ConfigureAwait(false))
            {
                return Failed("A previous AdBlock activation could not be recovered safely.");
            }

            var selectedPath = Canonicalize(selectedSource.SourceFolderPath);
            if (IsSameOrBelow(selectedPath, Path.GetDirectoryName(managedSourceDirectory)!))
            {
                return Failed("Select the original reviewed AdBlock folder, not the application-managed copy.");
            }

            var selectedValidation = await sourceInspector.InspectAsync(selectedPath, cancellationToken).ConfigureAwait(false);
            if (!selectedValidation.IsValid
                || selectedValidation.Identity is null
                || !HasSameTrustedPayload(selectedSource, selectedValidation.Identity))
            {
                return Failed("The selected AdBlock payload changed after validation or is no longer trusted.");
            }

            if (directoryOperations.Exists(managedSourceDirectory))
            {
                var existing = await sourceInspector.InspectAsync(managedSourceDirectory, cancellationToken).ConfigureAwait(false);
                if (existing.IsValid
                    && existing.Identity is not null
                    && HasSameTrustedPayload(selectedSource, existing.Identity))
                {
                    return new(true, existing.Identity, ReusedExisting: true, ErrorMessage: null);
                }
            }

            var sourceTree = await FileXAdBlockPayloadTree.InspectAsync(selectedPath, cancellationToken).ConfigureAwait(false);
            if (selectedSource.Payload != sourceTree.Identity)
            {
                return Failed("The selected AdBlock payload changed before staging.");
            }

            var parent = Path.GetDirectoryName(managedSourceDirectory)
                ?? throw new InvalidOperationException("The managed extension directory is unavailable.");
            Directory.CreateDirectory(parent);
            Directory.CreateDirectory(stagingPath);
            foreach (var file in sourceTree.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = Path.Combine(stagingPath, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                await fileCopier.CopyAsync(
                    file.FullPath,
                    file.Length,
                    file.Sha256,
                    destination,
                    cancellationToken).ConfigureAwait(false);
            }

            var staged = await sourceInspector.InspectAsync(stagingPath, cancellationToken).ConfigureAwait(false);
            if (!staged.IsValid
                || staged.Identity is null
                || !HasSameTrustedPayload(selectedSource, staged.Identity))
            {
                return Failed("The staged AdBlock payload did not match the reviewed trust record.");
            }

            if (directoryOperations.Exists(managedSourceDirectory))
            {
                directoryOperations.Move(managedSourceDirectory, backupPath);
                movedExistingToBackup = true;
            }

            directoryOperations.Move(stagingPath, managedSourceDirectory);
            activatedStaging = true;
            var installed = await sourceInspector.InspectAsync(managedSourceDirectory, cancellationToken).ConfigureAwait(false);
            if (!installed.IsValid
                || installed.Identity is null
                || !HasSameTrustedPayload(selectedSource, installed.Identity))
            {
                TryRestorePreviousPayload(backupPath, rejectedPath, movedExistingToBackup, activatedStaging);
                movedExistingToBackup = false;
                activatedStaging = false;
                return Failed("The final managed AdBlock payload failed trusted verification.");
            }

            movedExistingToBackup = false;
            activatedStaging = false;
            DeleteIfPresent(backupPath);

            return new(true, installed.Identity, ReusedExisting: false, ErrorMessage: null);
        }
        catch (OperationCanceledException)
        {
            if (movedExistingToBackup || activatedStaging)
            {
                TryRestorePreviousPayload(backupPath, rejectedPath, movedExistingToBackup, activatedStaging);
            }

            throw;
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or InvalidOperationException
                                           or InvalidDataException)
        {
            if (movedExistingToBackup || activatedStaging)
            {
                TryRestorePreviousPayload(backupPath, rejectedPath, movedExistingToBackup, activatedStaging);
            }

            return Failed("The AdBlock payload could not be staged and activated safely.");
        }
        finally
        {
            DeleteIfPresent(stagingPath);
            if (directoryOperations.Exists(managedSourceDirectory))
            {
                DeleteIfPresent(rejectedPath);
            }
        }
    }

    public async Task<bool> RecoverInterruptedActivationAsync(CancellationToken cancellationToken = default)
    {
        var stagingPath = $"{managedSourceDirectory}.staging";
        var backupPath = $"{managedSourceDirectory}.previous";
        var rejectedPath = $"{managedSourceDirectory}.rejected";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (directoryOperations.Exists(backupPath))
            {
                if (!directoryOperations.Exists(managedSourceDirectory))
                {
                    directoryOperations.Move(backupPath, managedSourceDirectory);
                }
                else
                {
                    var current = await sourceInspector.InspectAsync(
                        managedSourceDirectory,
                        cancellationToken).ConfigureAwait(false);
                    if (current.IsValid
                        && current.Identity is not null
                        && IsTrustedByStore(current.Identity))
                    {
                        DeleteIfPresent(backupPath);
                    }
                    else if (!TryRestorePreviousPayload(
                                 backupPath,
                                 rejectedPath,
                                 movedExistingToBackup: true,
                                 activatedStaging: true))
                    {
                        return false;
                    }
                }
            }

            DeleteIfPresent(stagingPath);
            if (directoryOperations.Exists(managedSourceDirectory))
            {
                DeleteIfPresent(rejectedPath);
            }

            return !directoryOperations.Exists(backupPath)
                || directoryOperations.Exists(managedSourceDirectory);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or InvalidOperationException
                                           or InvalidDataException)
        {
            return false;
        }
    }

    public Task<XAdBlockManagedPayloadCleanup> RemoveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!directoryOperations.Exists(managedSourceDirectory))
            {
                return Task.FromResult(new XAdBlockManagedPayloadCleanup(true, Removed: false, ErrorMessage: null));
            }

            EnsureTreeIsSafeForDeletion(managedSourceDirectory);
            directoryOperations.Delete(managedSourceDirectory, recursive: true);
            return Task.FromResult(new XAdBlockManagedPayloadCleanup(true, Removed: true, ErrorMessage: null));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Task.FromResult(new XAdBlockManagedPayloadCleanup(
                false,
                Removed: false,
                "AdBlock was removed from WebView2, but its application-managed payload could not be deleted."));
        }
    }

    private bool TryRestorePreviousPayload(
        string backupPath,
        string rejectedPath,
        bool movedExistingToBackup,
        bool activatedStaging)
    {
        try
        {
            if (activatedStaging && directoryOperations.Exists(managedSourceDirectory))
            {
                DeleteIfPresent(rejectedPath);
                if (directoryOperations.Exists(rejectedPath))
                {
                    return false;
                }

                directoryOperations.Move(managedSourceDirectory, rejectedPath);
            }

            if (movedExistingToBackup && directoryOperations.Exists(backupPath))
            {
                if (directoryOperations.Exists(managedSourceDirectory))
                {
                    return false;
                }

                directoryOperations.Move(backupPath, managedSourceDirectory);
            }

            return !movedExistingToBackup || directoryOperations.Exists(managedSourceDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    private void DeleteIfPresent(string path)
    {
        try
        {
            if (directoryOperations.Exists(path))
            {
                directoryOperations.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup failure must not replace the trusted Current payload or expose a private path.
        }
    }

    private bool HasSameTrustedPayload(
        XDiagnosticAdBlockSourceIdentity expected,
        XDiagnosticAdBlockSourceIdentity actual) =>
        IsTrustedByStore(expected)
        && IsTrustedByStore(actual)
        && expected.Payload == actual.Payload
        && expected.TrustRecord == actual.TrustRecord
        && expected.Manifest.HasSamePayload(actual.Manifest)
        && string.Equals(
            expected.Manifest.OfficialStoreExtensionId,
            actual.Manifest.OfficialStoreExtensionId,
            StringComparison.Ordinal);

    private bool IsTrustedByStore(XDiagnosticAdBlockSourceIdentity source) =>
        source.Payload is not null
        && source.TrustRecord is not null
        && trustRecords.Contains(source.TrustRecord)
        && source.TrustRecord.Matches(
            source.Manifest.OfficialStoreExtensionId,
            source.Manifest.Version,
            source.Payload);

    private static void EnsureRootIsSafeDirectory(string rootPath)
    {
        var root = new DirectoryInfo(rootPath);
        root.Refresh();
        if (!root.Exists || (root.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("The extension payload directory is unavailable or uses a reparse point.");
        }
    }

    private static void EnsureTreeIsSafeForDeletion(string rootPath)
    {
        EnsureRootIsSafeDirectory(rootPath);
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(rootPath));
        while (pending.Count > 0)
        {
            foreach (var entry in pending.Pop().EnumerateFileSystemInfos())
            {
                entry.Refresh();
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException(
                        "The managed extension payload contains a reparse point and cannot be removed safely.");
                }

                if (entry is DirectoryInfo directory)
                {
                    pending.Push(directory);
                }
            }
        }
    }

    private static XAdBlockManagedPayloadPreparation Failed(string message) =>
        new(false, Identity: null, ReusedExisting: false, message);

    private static bool IsSameOrBelow(string candidate, string root)
    {
        var canonicalRoot = Canonicalize(root);
        return string.Equals(candidate, canonicalRoot, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith($"{canonicalRoot}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static string Canonicalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
