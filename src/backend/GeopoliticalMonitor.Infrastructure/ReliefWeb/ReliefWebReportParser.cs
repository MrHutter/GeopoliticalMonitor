using System.Globalization;
using System.Text.Json;
using GeopoliticalMonitor.Application.ReliefWeb;

namespace GeopoliticalMonitor.Infrastructure.ReliefWeb;

public sealed class ReliefWebReportParser
{
    public const int MaximumReports = 20;

    public ReliefWebReportBatch ParseSearch(ReadOnlyMemory<byte> payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!TryGetData(document.RootElement, out var data))
            {
                throw InvalidResponse("ReliefWeb returned an unsupported response.");
            }

            var reports = new List<ReliefWebReportRecord>();
            var skipped = 0;
            var seen = 0;
            foreach (var item in data.EnumerateArray())
            {
                if (seen++ >= MaximumReports)
                {
                    break;
                }

                if (TryParseReport(item, requireOriginalDate: false, out var report))
                {
                    reports.Add(report!);
                }
                else
                {
                    skipped++;
                }
            }

            if (seen > 0 && reports.Count == 0)
            {
                throw InvalidResponse("ReliefWeb returned no valid reports in a nonempty response.");
            }

            return new ReliefWebReportBatch(reports, skipped);
        }
        catch (ReliefWebClientException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("ReliefWeb returned invalid JSON.", exception);
        }
    }

    public ReliefWebReportRecord ParseReport(ReadOnlyMemory<byte> payload, long expectedReportId)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!TryGetData(document.RootElement, out var data))
            {
                throw InvalidResponse("ReliefWeb returned an unsupported response.");
            }

            foreach (var item in data.EnumerateArray())
            {
                if (TryParseReport(item, requireOriginalDate: true, out var report)
                    && report!.ReportId == expectedReportId)
                {
                    return report;
                }
            }

            throw InvalidResponse("ReliefWeb returned an invalid report response.");
        }
        catch (ReliefWebClientException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw InvalidResponse("ReliefWeb returned invalid JSON.", exception);
        }
    }

    private static bool TryGetData(JsonElement root, out JsonElement data)
    {
        data = default;
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("data", out data)
            && data.ValueKind == JsonValueKind.Array;
    }

    private static bool TryParseReport(JsonElement item, bool requireOriginalDate, out ReliefWebReportRecord? report)
    {
        report = null;
        if (item.ValueKind != JsonValueKind.Object
            || !TryPositiveInt64(item, "id", out var id)
            || !item.TryGetProperty("fields", out var fields)
            || fields.ValueKind != JsonValueKind.Object
            || !TryText(fields, "title", 256, required: true, out var title)
            || !TryReliefWebUrl(fields, "url", required: true, out var canonicalUrl)
            || !TryHttpsUrl(fields, "origin", out var originUrl)
            || !TryDateContainer(fields, "date", requireOriginalDate, out var original, out var created, out var changed)
            || !TryOrganizations(fields, out var organizations)
            || !TryNamedList(fields, "format", 16, out var formats)
            || !TryCountries(fields, out var countries)
            || !TryDisasters(fields, out var disasters)
            || !TryNamedList(fields, "language", 16, out var languages))
        {
            return false;
        }

        report = new ReliefWebReportRecord(
            id,
            title!,
            canonicalUrl!,
            originUrl,
            original,
            created,
            changed,
            organizations,
            formats,
            countries,
            disasters,
            languages);
        return true;
    }

    private static bool TryDateContainer(
        JsonElement fields,
        string name,
        bool requireOriginal,
        out DateTimeOffset? original,
        out DateTimeOffset? created,
        out DateTimeOffset? changed)
    {
        original = null;
        created = null;
        changed = null;
        if (!fields.TryGetProperty(name, out var date) || date.ValueKind == JsonValueKind.Null)
        {
            return !requireOriginal;
        }

        return date.ValueKind == JsonValueKind.Object
            && TryOptionalUtc(date, "original", out original)
            && (!requireOriginal || original is not null)
            && TryOptionalUtc(date, "created", out created)
            && TryOptionalUtc(date, "changed", out changed);
    }

    private static bool TryOrganizations(JsonElement fields, out IReadOnlyList<ReliefWebOrganization> organizations)
    {
        organizations = [];
        if (!TryObjectArray(fields, "source", 8, out var items))
        {
            return false;
        }

        var values = new List<ReliefWebOrganization>();
        foreach (var item in items)
        {
            if (!TryOptionalPositiveInt64(item, "id", out var id)
                || !TryText(item, "name", 256, required: false, out var name)
                || !TryText(item, "shortname", 128, required: false, out var shortName))
            {
                return false;
            }

            var displayName = shortName ?? name;
            if (displayName is not null)
            {
                values.Add(new ReliefWebOrganization(id, displayName));
            }
        }

        organizations = values;
        return true;
    }

    private static bool TryCountries(JsonElement fields, out IReadOnlyList<ReliefWebCountry> countries)
    {
        countries = [];
        if (!TryObjectArray(fields, "country", 16, out var items))
        {
            return false;
        }

        var values = new List<ReliefWebCountry>();
        foreach (var item in items)
        {
            if (!TryOptionalPositiveInt64(item, "id", out var id)
                || !TryText(item, "name", 128, required: true, out var name)
                || !TryText(item, "iso3", 3, required: false, out var iso3)
                || (iso3 is not null && (iso3.Length != 3 || !iso3.All(char.IsAsciiLetter))))
            {
                return false;
            }

            values.Add(new ReliefWebCountry(id, name!, iso3?.ToUpperInvariant()));
        }

        countries = values;
        return true;
    }

    private static bool TryDisasters(JsonElement fields, out IReadOnlyList<ReliefWebDisaster> disasters)
    {
        disasters = [];
        if (!TryObjectArray(fields, "disaster", 16, out var items))
        {
            return false;
        }

        var values = new List<ReliefWebDisaster>();
        foreach (var item in items)
        {
            if (!TryPositiveInt64(item, "id", out var id)
                || !TryText(item, "name", 256, required: true, out var name)
                || !TryText(item, "glide", 64, required: false, out var glide)
                || !TryNamedList(item, "type", 8, out var types))
            {
                return false;
            }

            values.Add(new ReliefWebDisaster(id, name!, glide, types));
        }

        disasters = values;
        return true;
    }

    private static bool TryNamedList(JsonElement owner, string name, int maximumItems, out IReadOnlyList<string> values)
    {
        values = [];
        if (!TryObjectArray(owner, name, maximumItems, out var items))
        {
            return false;
        }

        var result = new List<string>();
        foreach (var item in items)
        {
            if (!TryText(item, "name", 128, required: true, out var value))
            {
                return false;
            }

            result.Add(value!);
        }

        values = result;
        return true;
    }

    private static bool TryObjectArray(JsonElement owner, string name, int maximumItems, out IReadOnlyList<JsonElement> values)
    {
        values = [];
        if (!owner.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array || property.GetArrayLength() > maximumItems)
        {
            return false;
        }

        var items = new List<JsonElement>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            items.Add(item);
        }

        values = items;
        return true;
    }

    private static bool TryReliefWebUrl(JsonElement owner, string name, bool required, out Uri? uri)
    {
        if (!TryUri(owner, name, required, out uri))
        {
            return false;
        }

        return uri is null
            || string.Equals(uri.Host, "reliefweb.int", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "www.reliefweb.int", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryHttpsUrl(JsonElement owner, string name, out Uri? uri)
        => TryUri(owner, name, required: false, out uri);

    private static bool TryUri(JsonElement owner, string name, bool required, out Uri? uri)
    {
        uri = null;
        if (!TryText(owner, name, 1024, required, out var value))
        {
            return false;
        }

        return value is null
            || (Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool TryOptionalUtc(JsonElement owner, string name, out DateTimeOffset? value)
    {
        value = null;
        if (!owner.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(property.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryText(JsonElement owner, string name, int maximumLength, bool required, out string? value)
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

        return value.Length <= maximumLength && !value.Contains('<') && !value.Contains('>');
    }

    private static bool TryPositiveInt64(JsonElement owner, string name, out long value)
    {
        value = 0;
        return owner.TryGetProperty(name, out var property)
            && ((property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out value))
                || (property.ValueKind == JsonValueKind.String
                    && long.TryParse(property.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out value)))
            && value > 0;
    }

    private static bool TryOptionalPositiveInt64(JsonElement owner, string name, out long? value)
    {
        value = null;
        if (!owner.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (!TryPositiveInt64(owner, name, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static ReliefWebClientException InvalidResponse(string message, Exception? innerException = null)
        => new(ReliefWebFailureKind.InvalidResponse, message, innerException);
}
