using System.Globalization;
using System.Text.Json;
using GeopoliticalMonitor.Application.Gdacs;

namespace GeopoliticalMonitor.Infrastructure.Gdacs;

public sealed class GdacsGeoJsonParser
{
    public const int MaximumRecords = 100;

    public GdacsEarthquakeFeedBatch Parse(ReadOnlyMemory<byte> payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !HasString(root, "type", "FeatureCollection")
                || !root.TryGetProperty("features", out var features)
                || features.ValueKind != JsonValueKind.Array)
            {
                throw InvalidResponse("GDACS returned an unsupported GeoJSON response.");
            }

            var records = new List<GdacsEarthquakeRecord>();
            var skipped = 0;
            var seen = 0;
            foreach (var feature in features.EnumerateArray())
            {
                if (seen++ >= MaximumRecords)
                {
                    break;
                }

                if (TryParseFeature(feature, out var record))
                {
                    records.Add(record!);
                }
                else
                {
                    skipped++;
                }
            }

            if (seen > 0 && records.Count == 0)
            {
                throw InvalidResponse("GDACS returned no valid earthquake records in a nonempty response.");
            }

            return new GdacsEarthquakeFeedBatch(records, skipped);
        }
        catch (GdacsFeedException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("GDACS returned invalid JSON.", exception);
        }
    }

    private static bool TryParseFeature(JsonElement feature, out GdacsEarthquakeRecord? record)
    {
        record = null;
        if (feature.ValueKind != JsonValueKind.Object
            || !HasString(feature, "type", "Feature")
            || !feature.TryGetProperty("geometry", out var geometry)
            || geometry.ValueKind != JsonValueKind.Object
            || !HasString(geometry, "type", "Point")
            || !geometry.TryGetProperty("coordinates", out var coordinates)
            || coordinates.ValueKind != JsonValueKind.Array
            || coordinates.GetArrayLength() < 2
            || !TryFiniteDouble(coordinates[0], -180, 180, out var longitude)
            || !TryFiniteDouble(coordinates[1], -90, 90, out var latitude)
            || !feature.TryGetProperty("properties", out var properties)
            || properties.ValueKind != JsonValueKind.Object
            || !HasString(properties, "eventtype", "EQ")
            || !TryPositiveInt64(properties, "eventid", out var eventId)
            || !TryPositiveInt64(properties, "episodeid", out var episodeId)
            || !TryBoundedText(properties, "name", 220, required: true, out var name)
            || !TryBoundedText(properties, "description", 2000, required: false, out var description)
            || !TryUtcTimestamp(properties, "fromdate", out var startedAtUtc)
            || !TryUtcTimestamp(properties, "datemodified", out var providerUpdatedAtUtc)
            || !TryBoundedText(properties, "alertlevel", 16, required: true, out var alertLevel)
            || !IsAlertLevel(alertLevel!)
            || !TryOptionalText(properties, "country", 128, out var country)
            || !TryOptionalIso3(properties, out var iso3)
            || !TryReportUrl(properties, out var canonicalUrl)
            || !TryOptionalMagnitude(properties, out var magnitude))
        {
            return false;
        }

        var summary = string.IsNullOrWhiteSpace(description) ? name! : description!;
        if (ContainsMarkup(name!) || ContainsMarkup(summary))
        {
            return false;
        }

        record = new GdacsEarthquakeRecord(
            eventId,
            episodeId,
            name!,
            summary,
            startedAtUtc,
            providerUpdatedAtUtc,
            alertLevel!,
            magnitude,
            country,
            iso3,
            canonicalUrl!,
            longitude,
            latitude);
        return true;
    }

    private static bool HasString(JsonElement owner, string name, string expected)
    {
        return owner.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String
            && string.Equals(property.GetString(), expected, StringComparison.Ordinal);
    }

    private static bool TryPositiveInt64(JsonElement owner, string name, out long value)
    {
        value = 0;
        return owner.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out value)
            && value > 0;
    }

    private static bool TryFiniteDouble(JsonElement property, double minimum, double maximum, out double value)
    {
        value = 0;
        return property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out value)
            && double.IsFinite(value)
            && value >= minimum
            && value <= maximum;
    }

    private static bool TryBoundedText(JsonElement owner, string name, int maximumLength, bool required, out string? value)
    {
        value = null;
        if (!owner.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return !required;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = null;
            return !required;
        }

        return value.Length <= maximumLength;
    }

    private static bool TryOptionalText(JsonElement owner, string name, int maximumLength, out string? value)
    {
        value = null;
        if (!owner.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = null;
            return true;
        }

        return value.Length <= maximumLength && !ContainsMarkup(value);
    }

    private static bool TryOptionalIso3(JsonElement properties, out string? iso3)
    {
        if (!TryOptionalText(properties, "iso3", 3, out iso3))
        {
            return false;
        }

        return iso3 is null || (iso3.Length == 3 && iso3.All(char.IsAsciiLetter));
    }

    private static bool TryUtcTimestamp(JsonElement owner, string name, out DateTimeOffset value)
    {
        value = default;
        if (!owner.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return DateTimeOffset.TryParse(
            property.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out value);
    }

    private static bool TryReportUrl(JsonElement properties, out Uri? url)
    {
        url = null;
        if (!properties.TryGetProperty("url", out var urls)
            || urls.ValueKind != JsonValueKind.Object
            || !urls.TryGetProperty("report", out var report)
            || report.ValueKind != JsonValueKind.String
            || !Uri.TryCreate(report.GetString(), UriKind.Absolute, out url))
        {
            return false;
        }

        return url.Scheme == Uri.UriSchemeHttps
            && string.Equals(url.Host, "www.gdacs.org", StringComparison.OrdinalIgnoreCase)
            && string.Equals(url.AbsolutePath, "/report.aspx", StringComparison.OrdinalIgnoreCase)
            && url.AbsoluteUri.Length <= 1024;
    }

    private static bool TryOptionalMagnitude(JsonElement properties, out double? magnitude)
    {
        magnitude = null;
        if (!properties.TryGetProperty("severitydata", out var severity) || severity.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (severity.ValueKind != JsonValueKind.Object
            || !severity.TryGetProperty("severity", out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (!TryFiniteDouble(value, 0, 10, out var parsed))
        {
            return false;
        }

        magnitude = parsed;
        return true;
    }

    private static bool IsAlertLevel(string value)
    {
        return value is "Green" or "Orange" or "Red";
    }

    private static bool ContainsMarkup(string value)
    {
        return value.Contains('<') || value.Contains('>');
    }

    private static GdacsFeedException InvalidResponse(string message, Exception? innerException = null)
    {
        return new GdacsFeedException(GdacsFeedFailureKind.InvalidResponse, message, innerException);
    }
}
