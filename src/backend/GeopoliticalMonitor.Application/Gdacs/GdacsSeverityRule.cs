using System.Globalization;
using GeopoliticalMonitor.Domain.Events;

namespace GeopoliticalMonitor.Application.Gdacs;

public static class GdacsSeverityRule
{
    public const string RuleId = "gdacs-eq-v1";
    public const string BackfillUnavailableInputs = "backfill: alert level unavailable";
    public const double GreenMediumMagnitude = 6.0;
    public const double RedCatastrophicMagnitude = 7.5;

    private const string AlertLevelLinePrefix = "GDACS alert level:";
    private const string MagnitudeLinePrefix = "Magnitude:";

    public static EventSeverityAssessment Assess(string alertLevel, double? magnitude)
    {
        var level = alertLevel switch
        {
            "Green" => magnitude is >= GreenMediumMagnitude ? EventSeverity.Medium : EventSeverity.Low,
            "Orange" => EventSeverity.High,
            "Red" => magnitude is >= RedCatastrophicMagnitude ? EventSeverity.Catastrophic : EventSeverity.Critical,
            _ => throw new ArgumentOutOfRangeException(nameof(alertLevel), "GDACS alert level is not supported."),
        };

        return new EventSeverityAssessment(level, RuleId, FormatInputs(alertLevel, magnitude));
    }

    public static string FormatInputs(string alertLevel, double? magnitude)
    {
        return magnitude is null
            ? $"alert={alertLevel}"
            : $"alert={alertLevel}; magnitude={magnitude.Value.ToString("0.0##", CultureInfo.InvariantCulture)}";
    }

    public static bool TryAssessFromProvenanceText(string? provenanceText, out EventSeverityAssessment? assessment)
    {
        assessment = null;
        if (string.IsNullOrWhiteSpace(provenanceText))
        {
            return false;
        }

        string? alertLevel = null;
        double? magnitude = null;
        foreach (var rawLine in provenanceText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith(AlertLevelLinePrefix, StringComparison.Ordinal))
            {
                alertLevel = line[AlertLevelLinePrefix.Length..].Trim();
            }
            else if (line.StartsWith(MagnitudeLinePrefix, StringComparison.Ordinal))
            {
                var value = line[MagnitudeLinePrefix.Length..].Trim();
                if (value.EndsWith(" M", StringComparison.Ordinal)
                    && double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    && double.IsFinite(parsed))
                {
                    magnitude = parsed;
                }
            }
        }

        if (alertLevel is not ("Green" or "Orange" or "Red"))
        {
            return false;
        }

        assessment = Assess(alertLevel, magnitude);
        return true;
    }
}
