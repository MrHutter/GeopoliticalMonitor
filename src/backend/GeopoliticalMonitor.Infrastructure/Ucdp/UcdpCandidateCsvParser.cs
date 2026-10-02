using System.Globalization;
using System.Text;
using GeopoliticalMonitor.Application.Ucdp;

namespace GeopoliticalMonitor.Infrastructure.Ucdp;

/// <summary>
/// RFC 4180 parser for UCDP Candidate CSV releases. Columns are resolved by header name, so reordered or extra columns
/// are tolerated. Malformed rows are skipped and counted, never thrown; a missing required column or too many rows is an
/// invalid response. Article text, headlines and geometry columns are never read.
/// </summary>
public sealed class UcdpCandidateCsvParser
{
    public const int MaximumFieldLength = 8192;
    public const int MaximumRowLength = 65_536;
    public const int MaximumFieldsPerRow = 256;
    public const int MaximumRows = 20_000;

    public static readonly IReadOnlyList<string> RequiredColumns =
    [
        "id", "code_status", "type_of_violence", "dyad_name", "side_a", "side_b", "where_prec", "where_coordinates",
        "latitude", "longitude", "country", "region", "date_prec", "date_start", "date_end", "best", "high", "low",
    ];

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public UcdpCandidateFeedBatch Parse(ReadOnlySpan<byte> payload)
    {
        return Parse(Utf8.GetString(payload));
    }

    public UcdpCandidateFeedBatch Parse(string text)
    {
        var reader = new CsvRecordReader(text.Length > 0 && text[0] == '﻿' ? text[1..] : text);

        CsvRecord? header = null;
        while (reader.TryRead(out var candidate))
        {
            if (!candidate.IsBlank)
            {
                header = candidate;
                break;
            }
        }

        if (header is null || header.Malformed)
        {
            throw InvalidResponse("UCDP release has no usable header.");
        }

        var columns = ResolveColumns(header.Fields);
        var records = new List<UcdpCandidateRecord>();
        var skipped = 0;
        var rows = 0;

        while (reader.TryRead(out var row))
        {
            if (row.IsBlank)
            {
                continue;
            }

            if (++rows > MaximumRows)
            {
                throw InvalidResponse("UCDP release exceeded the permitted row count.");
            }

            if (row.Malformed || row.Fields.Count != header.Fields.Count || !TryCreateRecord(row.Fields, columns, out var record))
            {
                skipped++;
                continue;
            }

            records.Add(record);
        }

        return new UcdpCandidateFeedBatch(records, skipped);
    }

    private static Columns ResolveColumns(IReadOnlyList<string> headerFields)
    {
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headerFields.Count; index++)
        {
            var name = headerFields[index].Trim();
            if (!indexes.TryAdd(name, index))
            {
                duplicates.Add(name);
            }
        }

        foreach (var required in RequiredColumns)
        {
            if (!indexes.ContainsKey(required) || duplicates.Contains(required))
            {
                throw InvalidResponse("UCDP release is missing a required column.");
            }
        }

        int? Optional(string name) => indexes.TryGetValue(name, out var index) && !duplicates.Contains(name) ? index : null;
        return new Columns(indexes, Optional("event_clarity"), Optional("number_of_sources"));
    }

    private static bool TryCreateRecord(IReadOnlyList<string> fields, Columns columns, out UcdpCandidateRecord record)
    {
        record = null!;
        string Field(string name) => fields[columns.Indexes[name]].Trim();

        if (!long.TryParse(Field("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) || id <= 0
            || !TryParseInt(Field("type_of_violence"), out var typeOfViolence)
            || !TryParseInt(Field("where_prec"), out var wherePrecision)
            || !TryParseInt(Field("date_prec"), out var datePrecision)
            || !TryParseInt(Field("best"), out var best)
            || !TryParseInt(Field("high"), out var high)
            || !TryParseInt(Field("low"), out var low)
            || !double.TryParse(Field("latitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(Field("longitude"), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            || !TryParseDate(Field("date_start"), out var dateStart)
            || !TryParseDate(Field("date_end"), out var dateEnd))
        {
            return false;
        }

        record = new UcdpCandidateRecord(
            id,
            Field("code_status"),
            typeOfViolence,
            Field("dyad_name"),
            Field("side_a"),
            Field("side_b"),
            wherePrecision,
            Field("where_coordinates"),
            latitude,
            longitude,
            Field("country"),
            Field("region"),
            datePrecision,
            dateStart,
            dateEnd,
            best,
            high,
            low,
            columns.EventClarity is { } clarity ? fields[clarity].Trim() : null,
            columns.NumberOfSources is { } sources ? fields[sources].Trim() : null);
        return true;
    }

    private static bool TryParseInt(string value, out int result)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    /// <summary>Accepts yyyy-MM-dd, optionally followed by a time part that is ignored.</summary>
    internal static bool TryParseDate(string value, out DateOnly date)
    {
        date = default;
        if (value.Length < 10 || (value.Length > 10 && value[10] is not (' ' or 'T')))
        {
            return false;
        }

        return DateOnly.TryParseExact(value.AsSpan(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static UcdpFeedException InvalidResponse(string message) => new(UcdpFeedFailureKind.InvalidResponse, message);

    private sealed record Columns(Dictionary<string, int> Indexes, int? EventClarity, int? NumberOfSources);

    private sealed class CsvRecord(List<string> fields, bool malformed, bool isBlank)
    {
        public IReadOnlyList<string> Fields { get; } = fields;
        public bool Malformed { get; } = malformed;
        public bool IsBlank { get; } = isBlank;
    }

    /// <summary>Streaming RFC 4180 record reader with field, row and field-count limits.</summary>
    private sealed class CsvRecordReader(string text)
    {
        private int position;

        public bool TryRead(out CsvRecord record)
        {
            record = null!;
            if (position >= text.Length)
            {
                return false;
            }

            var fields = new List<string>();
            var field = new StringBuilder();
            var malformed = false;
            var inQuotes = false;
            var fieldWasQuoted = false;
            var afterClosingQuote = false;
            var rowLength = 0;

            void EndField()
            {
                if (fields.Count >= MaximumFieldsPerRow)
                {
                    malformed = true;
                }
                else
                {
                    fields.Add(malformed ? string.Empty : field.ToString());
                }

                field.Clear();
                fieldWasQuoted = false;
                afterClosingQuote = false;
            }

            void Append(char value)
            {
                if (field.Length >= MaximumFieldLength)
                {
                    malformed = true;
                    return;
                }

                field.Append(value);
            }

            while (position < text.Length)
            {
                var current = text[position++];
                if (++rowLength > MaximumRowLength)
                {
                    malformed = true;
                }

                if (inQuotes)
                {
                    if (current == '"')
                    {
                        if (position < text.Length && text[position] == '"')
                        {
                            position++;
                            Append('"');
                        }
                        else
                        {
                            inQuotes = false;
                            afterClosingQuote = true;
                        }
                    }
                    else
                    {
                        Append(current);
                    }

                    continue;
                }

                if (current == ',')
                {
                    EndField();
                    continue;
                }

                if (current is '\r' or '\n')
                {
                    if (current == '\r' && position < text.Length && text[position] == '\n')
                    {
                        position++;
                    }

                    var blank = fields.Count == 0 && field.Length == 0 && !fieldWasQuoted;
                    EndField();
                    record = new CsvRecord(fields, malformed, blank);
                    return true;
                }

                if (current == '"')
                {
                    if (field.Length == 0 && !fieldWasQuoted && !afterClosingQuote)
                    {
                        inQuotes = true;
                        fieldWasQuoted = true;
                    }
                    else
                    {
                        // A quote inside an unquoted field, or after a closing quote, is not RFC 4180.
                        malformed = true;
                    }

                    continue;
                }

                if (afterClosingQuote)
                {
                    malformed = true;
                    continue;
                }

                Append(current);
            }

            // End of input: an unterminated quoted field makes the final record malformed.
            if (inQuotes)
            {
                malformed = true;
            }

            var isBlankAtEnd = fields.Count == 0 && field.Length == 0 && !fieldWasQuoted;
            EndField();
            record = new CsvRecord(fields, malformed, isBlankAtEnd);
            return true;
        }
    }
}
