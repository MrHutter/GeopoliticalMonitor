using System.Text;

namespace GeopoliticalMonitor.Application.Ucdp;

/// <summary>
/// Normalizes untrusted provider text before persistence (design section 12): Unicode NFC, control characters removed,
/// whitespace collapsed, and truncation to a database limit without splitting a surrogate pair.
/// </summary>
public static class ProviderTextSanitizer
{
    /// <summary>One line of text. Line breaks and tabs become single spaces.</summary>
    public static string SingleLine(string? value, int maximumLength)
    {
        return Truncate(CollapseLine(Normalize(value)), maximumLength);
    }

    /// <summary>Text that keeps line breaks (as LF) between non-empty lines; everything else as in <see cref="SingleLine"/>.</summary>
    public static string MultiLine(string? value, int maximumLength)
    {
        var normalized = Normalize(value).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized
            .Split('\n')
            .Select(CollapseLine)
            .Where(line => line.Length > 0);
        return Truncate(string.Join('\n', lines), maximumLength);
    }

    public static string Truncate(string value, int maximumLength)
    {
        if (value.Length <= maximumLength)
        {
            return value;
        }

        var length = maximumLength;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }

        return value[..length].TrimEnd();
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Lone surrogates make NFC normalization throw, so replace them first.
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsHighSurrogate(current) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
            {
                builder.Append(current).Append(value[index + 1]);
                index++;
            }
            else if (char.IsSurrogate(current))
            {
                builder.Append('�');
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string CollapseLine(string line)
    {
        var builder = new StringBuilder(line.Length);
        var pendingSpace = false;
        foreach (var current in line)
        {
            if (char.IsWhiteSpace(current))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (char.IsControl(current) || IsBidiControl(current))
            {
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool IsBidiControl(char value)
        => value is >= '‪' and <= '‮' or >= '⁦' and <= '⁩' or '‎' or '‏' or '؜';
}
