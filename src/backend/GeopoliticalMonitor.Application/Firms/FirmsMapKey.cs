namespace GeopoliticalMonitor.Application.Firms;

/// <summary>
/// A normalized NASA FIRMS MAP_KEY. It exists only in native code: ToString never returns the value, so the key cannot
/// leak through string interpolation, record printing or exception messages. Only the FIRMS client (for its request to
/// the fixed official host) and the OS key store (to write it) call <see cref="RevealForProviderRequest"/>.
/// </summary>
public sealed class FirmsMapKey
{
    /// <summary>Far above a real 32-character key and well inside the Windows credential blob limit.</summary>
    public const int MaximumLength = 512;
    public const int ExpectedLength = 32;
    public const string RedactedText = "[redacted FIRMS key]";

    private readonly string value;

    private FirmsMapKey(string value)
    {
        this.value = value;
    }

    /// <summary>True when the key is exactly 32 hexadecimal characters. Anything else is a warning, never a block.</summary>
    public bool HasExpectedFormat => IsExpectedFormat(value);

    /// <summary>Normalizes input and returns null when it is empty or longer than <see cref="MaximumLength"/>.</summary>
    public static FirmsMapKey? FromInput(string? input)
    {
        var normalized = Normalize(input);
        return normalized is null || normalized.Length > MaximumLength ? null : new FirmsMapKey(normalized);
    }

    /// <summary>Trims whitespace, strips one pair of matching surrounding single or double quotes, and trims again.</summary>
    public static string? Normalize(string? input)
    {
        if (input is null)
        {
            return null;
        }

        var text = input.Trim();
        if (text.Length >= 2 && text[0] == text[^1] && text[0] is '"' or '\'')
        {
            text = text[1..^1].Trim();
        }

        return text.Length == 0 ? null : text;
    }

    public static bool IsExpectedFormat(string? normalized)
        => normalized is { Length: ExpectedLength } && normalized.All(Uri.IsHexDigit);

    public string RevealForProviderRequest() => value;

    public override string ToString() => RedactedText;
}

public enum FirmsKeySource
{
    None,
    App,
    Environment,
}

public enum FirmsKeyStoreOutcome
{
    Succeeded,
    NotFound,
    Unavailable,
}

public sealed record FirmsKeyStoreRead(FirmsKeyStoreOutcome Outcome, FirmsMapKey? Key = null);

/// <summary>
/// The single app-stored FIRMS key in native OS-protected storage. The credential identifier is fixed by the
/// implementation and is never supplied by the UI. Implementations return bounded outcomes instead of throwing.
/// </summary>
public interface IFirmsKeyStore
{
    FirmsKeyStoreRead Read();

    FirmsKeyStoreOutcome Write(FirmsMapKey key);

    FirmsKeyStoreOutcome Delete();
}

public sealed record FirmsResolvedKey(FirmsKeySource Source, FirmsMapKey? Key, bool StoreUnavailable);

public interface IFirmsKeyResolver
{
    /// <summary>Resolves the key for one use. Callers must not cache the result beyond that use.</summary>
    FirmsResolvedKey Resolve();
}

/// <summary>The app-stored key wins; the environment variable is the fallback; otherwise FIRMS is not configured.</summary>
public sealed class FirmsKeyResolver(IFirmsKeyStore store, Func<string?> readEnvironmentKey) : IFirmsKeyResolver
{
    public FirmsResolvedKey Resolve()
    {
        var stored = FirmsKeyAccess.Read(store);
        if (stored.Outcome == FirmsKeyStoreOutcome.Succeeded && stored.Key is not null)
        {
            return new FirmsResolvedKey(FirmsKeySource.App, stored.Key, false);
        }

        var storeUnavailable = stored.Outcome == FirmsKeyStoreOutcome.Unavailable;
        string? environmentValue;
        try
        {
            environmentValue = readEnvironmentKey();
        }
        catch (Exception)
        {
            environmentValue = null;
        }

        var environmentKey = FirmsMapKey.FromInput(environmentValue);
        return environmentKey is null
            ? new FirmsResolvedKey(FirmsKeySource.None, null, storeUnavailable)
            : new FirmsResolvedKey(FirmsKeySource.Environment, environmentKey, storeUnavailable);
    }
}

public sealed record FirmsKeyStatus(bool Configured, FirmsKeySource Source, bool FormatWarning, bool StoreUnavailable);

public enum FirmsKeyChangeOutcome
{
    Saved,
    Removed,
    Cancelled,
    EmptyKey,
    KeyTooLong,
    NotStored,
    StoreUnavailable,
    Busy,
}

/// <summary>Status, set and remove for the app-stored FIRMS key. Status reports only flags and the source.</summary>
public sealed class FirmsKeyService(IFirmsKeyStore store, IFirmsKeyResolver resolver)
{
    public FirmsKeyStatus GetStatus()
    {
        var resolved = resolver.Resolve();
        return new FirmsKeyStatus(
            resolved.Key is not null,
            resolved.Source,
            resolved.Key is { HasExpectedFormat: false },
            resolved.StoreUnavailable);
    }

    /// <summary>True when the entered text would be saved but is not 32 hexadecimal characters.</summary>
    public static bool ShouldWarn(string? input)
    {
        var normalized = FirmsMapKey.Normalize(input);
        return normalized is not null && !FirmsMapKey.IsExpectedFormat(normalized);
    }

    public FirmsKeyChangeOutcome SetKey(string? input)
    {
        var normalized = FirmsMapKey.Normalize(input);
        if (normalized is null)
        {
            return FirmsKeyChangeOutcome.EmptyKey;
        }

        var key = FirmsMapKey.FromInput(normalized);
        if (key is null)
        {
            return FirmsKeyChangeOutcome.KeyTooLong;
        }

        return FirmsKeyAccess.Write(store, key) == FirmsKeyStoreOutcome.Succeeded
            ? FirmsKeyChangeOutcome.Saved
            : FirmsKeyChangeOutcome.StoreUnavailable;
    }

    /// <summary>Deletes only the app-stored key. The environment variable is never changed.</summary>
    public FirmsKeyChangeOutcome RemoveAppKey() => FirmsKeyAccess.Delete(store) switch
    {
        FirmsKeyStoreOutcome.Succeeded => FirmsKeyChangeOutcome.Removed,
        FirmsKeyStoreOutcome.NotFound => FirmsKeyChangeOutcome.NotStored,
        _ => FirmsKeyChangeOutcome.StoreUnavailable,
    };
}

/// <summary>Turns any store exception into a bounded outcome so a broken store never stops the app.</summary>
internal static class FirmsKeyAccess
{
    public static FirmsKeyStoreRead Read(IFirmsKeyStore store)
    {
        try
        {
            return store.Read();
        }
        catch (Exception)
        {
            return new FirmsKeyStoreRead(FirmsKeyStoreOutcome.Unavailable);
        }
    }

    public static FirmsKeyStoreOutcome Write(IFirmsKeyStore store, FirmsMapKey key)
    {
        try
        {
            return store.Write(key);
        }
        catch (Exception)
        {
            return FirmsKeyStoreOutcome.Unavailable;
        }
    }

    public static FirmsKeyStoreOutcome Delete(IFirmsKeyStore store)
    {
        try
        {
            return store.Delete();
        }
        catch (Exception)
        {
            return FirmsKeyStoreOutcome.Unavailable;
        }
    }
}
