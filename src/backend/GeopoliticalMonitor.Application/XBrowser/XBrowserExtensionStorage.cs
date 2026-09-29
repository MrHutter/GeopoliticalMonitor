namespace GeopoliticalMonitor.Application.XBrowser;

public sealed class XBrowserExtensionStorage
{
    public const string ManifestFileName = "manifest.json";
    public const string ManagedExtensionDirectoryName = "ManagedAdBlockExtension";

    public string PrepareForInstall(string sourceFolderPath, string stableExtensionsRoot)
    {
        var sourceRoot = ValidateSourceFolder(sourceFolderPath);
        var stableRoot = EnsureStableRoot(stableExtensionsRoot);
        var destinationRoot = Path.Combine(stableRoot, ManagedExtensionDirectoryName);
        var destinationFullPath = Path.GetFullPath(destinationRoot);

        EnsurePathIsWithinRoot(destinationFullPath, stableRoot);
        RejectReparsePointTree(sourceRoot);

        if (string.Equals(sourceRoot, destinationFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return destinationFullPath;
        }

        if (Directory.Exists(destinationFullPath))
        {
            RejectReparsePointTree(destinationFullPath);
            Directory.Delete(destinationFullPath, recursive: true);
        }

        CopyDirectory(sourceRoot, destinationFullPath, sourceRoot);
        return destinationFullPath;
    }

    public static string ValidateSourceFolder(string sourceFolderPath)
    {
        if (string.IsNullOrWhiteSpace(sourceFolderPath))
        {
            throw new InvalidOperationException("Select an extension folder.");
        }

        var sourceRoot = Path.GetFullPath(sourceFolderPath);
        if (!Directory.Exists(sourceRoot))
        {
            throw new InvalidOperationException("The selected extension folder does not exist.");
        }

        var manifestPath = Path.Combine(sourceRoot, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException("The selected folder must contain manifest.json at its top level.");
        }

        RejectReparsePoint(sourceRoot);
        return sourceRoot;
    }

    private static string EnsureStableRoot(string stableExtensionsRoot)
    {
        if (string.IsNullOrWhiteSpace(stableExtensionsRoot))
        {
            throw new InvalidOperationException("The X extension storage directory is unavailable.");
        }

        var stableRoot = Path.GetFullPath(stableExtensionsRoot);
        Directory.CreateDirectory(stableRoot);
        RejectReparsePoint(stableRoot);
        return stableRoot;
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot, string copyRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var file in Directory.EnumerateFiles(sourceRoot))
        {
            RejectReparsePoint(file);
            EnsurePathIsWithinRoot(file, copyRoot);
            var destinationPath = Path.Combine(destinationRoot, Path.GetFileName(file));
            File.Copy(file, destinationPath, overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceRoot))
        {
            RejectReparsePoint(directory);
            EnsurePathIsWithinRoot(directory, copyRoot);
            CopyDirectory(directory, Path.Combine(destinationRoot, Path.GetFileName(directory)), copyRoot);
        }
    }

    private static void RejectReparsePointTree(string root)
    {
        RejectReparsePoint(root);
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                RejectReparsePoint(entry);
                EnsurePathIsWithinRoot(entry, root);
                if (Directory.Exists(entry))
                {
                    pending.Push(entry);
                }
            }
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
        {
            throw new InvalidOperationException("Extension folders must not contain links or reparse points.");
        }
    }

    private static void EnsurePathIsWithinRoot(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.GetFullPath(root);
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (relative == "." || (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative)))
        {
            return;
        }

        throw new InvalidOperationException("Extension files must stay inside the selected extension folder.");
    }
}