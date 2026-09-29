using System.Text.Json;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Desktop;

public sealed class FileXDiagnosticAdBlockBindingStore(string bindingPath) : IXDiagnosticAdBlockBindingStore
{
    private const long MaximumBindingFileBytes = 32 * 1024;

    public bool HasPersistedState => File.Exists(bindingPath);

    public async Task<XDiagnosticAdBlockBinding?> GetAsync(CancellationToken cancellationToken = default)
    {
        var file = new FileInfo(bindingPath);
        if (!file.Exists || file.Length == 0 || file.Length > MaximumBindingFileBytes)
        {
            return null;
        }

        try
        {
            await using var stream = file.OpenRead();
            var binding = await JsonSerializer.DeserializeAsync<XDiagnosticAdBlockBinding>(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return binding?.IsStructurallyValid() == true ? binding : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task SaveAsync(
        XDiagnosticAdBlockBinding binding,
        CancellationToken cancellationToken = default)
    {
        if (!binding.IsStructurallyValid())
        {
            throw new InvalidDataException("The AdBlock binding is invalid.");
        }

        var directory = Path.GetDirectoryName(bindingPath)
            ?? throw new InvalidOperationException("The extension binding directory is unavailable.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{bindingPath}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, binding, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, bindingPath, overwrite: true);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(bindingPath))
        {
            File.Delete(bindingPath);
        }

        return Task.CompletedTask;
    }
}
