using System.Globalization;
using System.Text;
using GeopoliticalMonitor.Application.Firms;

namespace GeopoliticalMonitor.Infrastructure.Firms;

public sealed class FirmsCsvParser
{
    public const int MaximumCsvRows = 25_000;
    private const int MaximumTextLength = 32;
    private static readonly string[] RequiredHeaders =
    [
        "latitude", "longitude", "bright_ti4", "scan", "track", "acq_date", "acq_time",
        "satellite", "instrument", "confidence", "version", "bright_ti5", "frp", "daynight",
    ];

    public FirmsObservationBatch Parse(ReadOnlyMemory<byte> payload, FirmsSource source)
    {
        string csv;
        try
        {
            csv = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(payload.Span);
        }
        catch (DecoderFallbackException exception)
        {
            throw InvalidResponse("NASA FIRMS returned invalid UTF-8.", exception);
        }

        using var reader = new StringReader(csv);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine)
            || !TryParseCsvLine(headerLine.TrimStart('\uFEFF'), out var headerFields))
        {
            throw InvalidResponse("NASA FIRMS returned an invalid CSV header.");
        }

        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headerFields.Count; index++)
        {
            var name = headerFields[index].Trim();
            if (name.Length == 0 || !headers.TryAdd(name, index))
            {
                throw InvalidResponse("NASA FIRMS returned duplicate or empty CSV headers.");
            }
        }

        if (RequiredHeaders.Any(header => !headers.ContainsKey(header)))
        {
            throw InvalidResponse("NASA FIRMS returned an unsupported CSV schema.");
        }

        var observations = new List<FirmsObservation>();
        var skipped = 0;
        var dataRows = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            dataRows++;
            if (dataRows > MaximumCsvRows)
            {
                throw new FirmsFeedException(
                    FirmsFeedFailureKind.TooManyObservations,
                    "NASA FIRMS returned too many observations.");
            }

            if (!TryParseCsvLine(line, out var fields)
                || fields.Count != headerFields.Count
                || !TryParseObservation(fields, headers, source, out var observation))
            {
                skipped++;
                continue;
            }

            observations.Add(observation!);
        }

        if (dataRows > 0 && observations.Count == 0)
        {
            throw InvalidResponse("NASA FIRMS returned no valid observations in a nonempty response.");
        }

        return new FirmsObservationBatch(observations, skipped);
    }

    private static bool TryParseObservation(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> headers,
        FirmsSource source,
        out FirmsObservation? observation)
    {
        observation = null;
        if (!TryFiniteDouble(Value("latitude"), -90, 90, out var latitude)
            || !TryFiniteDouble(Value("longitude"), -180, 180, out var longitude)
            || !TryPositiveDouble(Value("bright_ti4"), out var brightI4)
            || !TryOptionalPositiveDouble(Value("bright_ti5"), out var brightI5)
            || !TryPositiveDouble(Value("scan"), out var scan)
            || !TryPositiveDouble(Value("track"), out var track)
            || !TryNonNegativeDouble(Value("frp"), out var frp)
            || !TryAcquiredAt(Value("acq_date"), Value("acq_time"), out var acquiredAtUtc)
            || !TryBoundedToken(Value("satellite"), MaximumTextLength, out var satellite)
            || !string.Equals(Value("instrument").Trim(), "VIIRS", StringComparison.OrdinalIgnoreCase)
            || !TryConfidence(Value("confidence"), out var confidence)
            || !TryBoundedText(Value("version"), MaximumTextLength, out var version)
            || !TryDayNight(Value("daynight"), out var dayNight))
        {
            return false;
        }

        var providerSource = source.ToProviderValue();
        observation = new FirmsObservation(
            FirmsObservationIdentity.Create(providerSource, satellite!, acquiredAtUtc, latitude, longitude),
            latitude,
            longitude,
            acquiredAtUtc,
            providerSource,
            satellite!,
            confidence!,
            frp,
            brightI4,
            brightI5,
            scan,
            track,
            version!,
            dayNight!);
        return true;

        string Value(string name) => fields[headers[name]];
    }

    private static bool TryParseCsvLine(string line, out IReadOnlyList<string> fields)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(builder.ToString());
                builder.Clear();
            }
            else
            {
                builder.Append(character);
            }
        }

        if (quoted)
        {
            fields = [];
            return false;
        }

        values.Add(builder.ToString());
        fields = values;
        return true;
    }

    private static bool TryAcquiredAt(string dateValue, string timeValue, out DateTimeOffset acquiredAtUtc)
    {
        acquiredAtUtc = default;
        var time = timeValue.Trim();
        if (time.Length is < 1 or > 4 || !time.All(char.IsAsciiDigit))
        {
            return false;
        }

        time = time.PadLeft(4, '0');
        return DateTimeOffset.TryParseExact(
            $"{dateValue.Trim()} {time}",
            "yyyy-MM-dd HHmm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out acquiredAtUtc);
    }

    private static bool TryConfidence(string value, out string? confidence)
    {
        confidence = value.Trim().ToLowerInvariant() switch
        {
            "l" or "low" => "Low",
            "n" or "nominal" => "Nominal",
            "h" or "high" => "High",
            _ => null,
        };
        return confidence is not null;
    }

    private static bool TryDayNight(string value, out string? dayNight)
    {
        dayNight = value.Trim().ToUpperInvariant() switch
        {
            "D" => "Day",
            "N" => "Night",
            _ => null,
        };
        return dayNight is not null;
    }

    private static bool TryBoundedToken(string value, int maximumLength, out string? normalized)
    {
        normalized = value.Trim();
        return normalized.Length > 0 && normalized.Length <= maximumLength
            && normalized.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }

    private static bool TryBoundedText(string value, int maximumLength, out string? normalized)
    {
        normalized = value.Trim();
        return normalized.Length > 0 && normalized.Length <= maximumLength
            && !normalized.Any(char.IsControl)
            && !normalized.Contains('<')
            && !normalized.Contains('>');
    }

    private static bool TryFiniteDouble(string value, double minimum, double maximum, out double parsed)
    {
        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
            && double.IsFinite(parsed)
            && parsed >= minimum
            && parsed <= maximum;
    }

    private static bool TryPositiveDouble(string value, out double parsed)
        => TryFiniteDouble(value, double.Epsilon, double.MaxValue, out parsed);

    private static bool TryNonNegativeDouble(string value, out double parsed)
        => TryFiniteDouble(value, 0, double.MaxValue, out parsed);

    private static bool TryOptionalPositiveDouble(string value, out double? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TryPositiveDouble(value, out var number))
        {
            return false;
        }

        parsed = number;
        return true;
    }

    private static FirmsFeedException InvalidResponse(string message, Exception? exception = null)
        => new(FirmsFeedFailureKind.InvalidProviderResponse, message, exception);
}
