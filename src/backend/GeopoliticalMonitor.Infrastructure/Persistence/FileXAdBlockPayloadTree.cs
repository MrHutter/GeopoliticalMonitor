using System.Security.Cryptography;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

internal sealed record XAdBlockRuntimeFile(
    string RelativePath,
    string FullPath,
    long Length,
    DateTime LastWriteTimeUtc,
    byte[] Sha256);

internal sealed record XAdBlockPayloadTreeInspection(
    XAdBlockPayloadIdentity Identity,
    IReadOnlyList<XAdBlockRuntimeFile> Files);

internal static class FileXAdBlockPayloadTree
{
    private const string BrowserMetadataDirectoryName = "_metadata";
    private const int BufferSize = 128 * 1024;

    public static async Task<XAdBlockPayloadTreeInspection> InspectAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        var root = new DirectoryInfo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath)));
        root.Refresh();
        if (!root.Exists || IsReparsePoint(root))
        {
            throw new InvalidDataException("The extension payload root is unavailable or uses a reparse point.");
        }

        var pending = new Stack<(DirectoryInfo Directory, int Depth)>();
        pending.Push((root, 0));
        var discoveredFiles = new List<(string RelativePath, FileInfo File)>();
        var budget = new XAdBlockPayloadResourceBudget();

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directory, depth) = pending.Pop();
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                entry.Refresh();
                if (IsReparsePoint(entry))
                {
                    throw new InvalidDataException("Extension payloads containing links or reparse points are not supported.");
                }

                if (entry is DirectoryInfo childDirectory)
                {
                    if (depth == 0
                        && string.Equals(childDirectory.Name, BrowserMetadataDirectoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var childDepth = depth + 1;
                    budget.AddDirectory(childDepth);
                    pending.Push((childDirectory, childDepth));
                    continue;
                }

                if (entry is not FileInfo file)
                {
                    throw new InvalidDataException("The extension payload contains a non-regular filesystem entry.");
                }

                var relativePath = XAdBlockPayloadDigest.NormalizeRelativePath(
                    Path.GetRelativePath(root.FullName, file.FullName));
                file.Refresh();
                budget.AddFile(relativePath, file.Length);
                discoveredFiles.Add((relativePath, file));
            }
        }

        var runtimeFiles = new List<XAdBlockRuntimeFile>(discoveredFiles.Count);
        foreach (var (relativePath, file) in discoveredFiles)
        {
            var initialLength = file.Length;
            var initialWriteTime = file.LastWriteTimeUtc;
            var (hash, bytesRead) = await HashFileAsync(file.FullName, cancellationToken).ConfigureAwait(false);
            file.Refresh();
            if (!file.Exists
                || IsReparsePoint(file)
                || bytesRead != initialLength
                || file.Length != initialLength
                || file.LastWriteTimeUtc != initialWriteTime)
            {
                throw new InvalidDataException("The extension payload changed during inspection.");
            }

            runtimeFiles.Add(new(relativePath, file.FullName, initialLength, initialWriteTime, hash));
        }

        var digest = XAdBlockPayloadDigest.Compute(runtimeFiles.Select(file =>
            new XAdBlockPayloadFileHash(file.RelativePath, file.Length, file.Sha256)));
        return new(
            new XAdBlockPayloadIdentity(
                XAdBlockPayloadDigest.DigestFormat,
                digest,
                runtimeFiles.Count,
                budget.TotalBytes,
                budget.DirectoryCount,
                budget.MaximumDirectoryDepth,
                budget.LongestPathUtf8Bytes),
            runtimeFiles.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray());
    }

    public static async Task<byte[]> ReadBoundedFileAsync(
        XAdBlockRuntimeFile file,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0 || file.Length > maximumBytes)
        {
            throw new InvalidDataException("Required extension metadata exceeds its configured size limit.");
        }

        var buffer = new byte[checked((int)file.Length)];
        await using var stream = OpenRead(file.FullPath);
        await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (stream.ReadByte() != -1
            || !SHA256.HashData(buffer).AsSpan().SequenceEqual(file.Sha256))
        {
            throw new InvalidDataException("Required extension metadata changed during inspection.");
        }

        return buffer;
    }

    public static async Task CopyFileAsync(
        XAdBlockRuntimeFile source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidDataException("The managed payload destination is invalid.");
        Directory.CreateDirectory(parent);
        await using var input = OpenRead(source.FullPath);
        await using var output = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[BufferSize];
        long copied = 0;
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            copied = checked(copied + count);
            if (copied > source.Length || copied > XAdBlockPayloadLimits.MaximumIndividualFileBytes)
            {
                throw new InvalidDataException("The extension payload changed or exceeded limits while copying.");
            }

            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }

        if (copied != source.Length)
        {
            throw new InvalidDataException("The extension payload changed while copying.");
        }
    }

    private static async Task<(byte[] Hash, long BytesRead)> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var stream = OpenRead(path);
        var buffer = new byte[BufferSize];
        long bytesRead = 0;
        while (true)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            bytesRead = checked(bytesRead + count);
            if (bytesRead > XAdBlockPayloadLimits.MaximumIndividualFileBytes)
            {
                throw new InvalidDataException("The extension payload contains a file larger than the configured limit.");
            }

            hash.AppendData(buffer.AsSpan(0, count));
        }

        return (hash.GetHashAndReset(), bytesRead);
    }

    private static FileStream OpenRead(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        BufferSize,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static bool IsReparsePoint(FileSystemInfo entry) =>
        (entry.Attributes & FileAttributes.ReparsePoint) != 0;
}
