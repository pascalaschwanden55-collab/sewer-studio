using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;

/// <summary>
/// Liest Befundzeilen und verbindet eindeutige Start-/Endzeilen von
/// Streckenschaeden. Dokumentkopf und Haltungszuordnung bleiben getrennt.
/// </summary>
internal static partial class TrainingPdfProtocolFindingParser
{
    [GeneratedRegex(
        @"^\s*(?<meter>\d{1,4}[.,]\d{1,3})[ \t]+(?:(?<operator>[AB]\d{1,3})[ \t]+)?(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})*)[ \t]+(?<description>.+?)[ \t]+(?<time>\d{2}:\d{2}:\d{2})(?:[ \t]+.*)?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ObservationTimeAfterTextRegex();

    [GeneratedRegex(
        @"^\s*(?:\d{1,5}[ \t]+)?(?<time>\d{2}:\d{2}:\d{2})[ \t]+(?<meter>\d{1,4}[.,]\d{1,3})[ \t]+(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})*)[ \t]+(?<description>.+?)\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ObservationTimeBeforeMeterRegex();

    [GeneratedRegex(
        @"^\s*(?<meter>\d{1,4}[.,]\d{1,3})[ \t]+(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})*)[ \t]+(?:\d{1,5}[ \t]+)?(?<time>\d{2}:\d{2}:\d{2})[ \t]+(?<description>.+?)\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ObservationTimeBeforeTextRegex();

    [GeneratedRegex(@"\b(?<marker>Start|Ende)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StretchMarkerRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonWordRegex();

    [GeneratedRegex(@"[ \t]{2,}\d{1,10}_[A-Fa-f0-9-]{4,}\s*$")]
    private static partial Regex WrappedTechnicalSuffixRegex();

    private static readonly string[] NonDescriptionPrefixes =
    [
        "--- seite",
        "seite ",
        "haltungsinspektion",
        "haltungsbilder",
        "fretz kanal",
        "projektname",
        "speicherstelle",
        "witterung",
        "kamera",
        "ort ",
        "strasse",
        "straße",
        "lage ",
        "profil ",
        "funktion ",
        "nutzungsart",
        "film ",
        "material ",
        "bemerkungen",
        "m +",
    ];

    public static IReadOnlyList<TrainingPdfProtocolFinding> Parse(string text)
    {
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var rows = new List<MutableObservation>();
        MutableObservation? current = null;

        foreach (var rawLine in lines)
        {
            var line = Regex.Replace(rawLine.Trim(), @"\s+", " ");
            var parsed = TryParseObservation(line);
            if (parsed is not null)
            {
                if (current is not null)
                    rows.Add(current);
                current = parsed;
                continue;
            }

            if (current is null)
                continue;
            var continuation = CleanDescriptionContinuation(rawLine);
            if (IsSafeDescriptionContinuation(continuation))
            {
                current.Description = $"{current.Description} {continuation}".Trim();
                continue;
            }

            rows.Add(current);
            current = null;
        }

        if (current is not null)
            rows.Add(current);

        PairStretchDamage(rows);
        return rows
            .Select(row => new TrainingPdfProtocolFinding(
                row.VsaCode,
                row.ObservationMeter,
                row.ObservationTime,
                row.Description,
                row.MeterStart,
                row.MeterEnd,
                row.IsStreckenschaden))
            .ToArray();
    }

    private static MutableObservation? TryParseObservation(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var match = ObservationTimeAfterTextRegex().Match(line);
        if (!match.Success)
            match = ObservationTimeBeforeMeterRegex().Match(line);
        if (!match.Success)
            match = ObservationTimeBeforeTextRegex().Match(line);
        if (!match.Success)
            return null;

        var code = GroundTruthFieldParser.NormalizeKnownVsaCode(
            match.Groups["code"].Value);
        if (code is null
            || !TryParseMeter(match.Groups["meter"].Value, out var meter)
            || !TimeSpan.TryParseExact(
                match.Groups["time"].Value,
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture,
                out var time))
        {
            return null;
        }

        var description = Regex.Replace(
            match.Groups["description"].Value.Trim(),
            @"\s+",
            " ");
        if (description.Length < 2)
            return null;

        return new MutableObservation(
            code,
            match.Groups["operator"].Success
                ? match.Groups["operator"].Value.ToUpperInvariant()
                : null,
            meter,
            time,
            description);
    }

    private static bool IsSafeDescriptionContinuation(string line)
    {
        if (line.Length < 3
            || NonDescriptionPrefixes.Any(prefix =>
                line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || TimeSpan.TryParse(line, CultureInfo.InvariantCulture, out _)
            || Regex.IsMatch(line, @"^[\d\s.,:/_+\-]+$")
            || Regex.IsMatch(line, @"^[A-Fa-f0-9][A-Fa-f0-9_\-]{5,}$")
            || Regex.IsMatch(line, @"\.(?:jpe?g|png)\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(
                line,
                @"^\d[\d.]*[-/]\d[\d.]*[_\-]",
                RegexOptions.IgnoreCase))
        {
            return false;
        }

        var words = NonWordRegex()
            .Split(line)
            .Where(word => word.Length >= 2)
            .ToArray();
        return words.Length >= 2
               && line.Any(char.IsLetter)
               && line.Any(char.IsLower);
    }

    private static string CleanDescriptionContinuation(string rawLine)
    {
        var withoutTechnicalSuffix = WrappedTechnicalSuffixRegex()
            .Replace(rawLine.Trim(), string.Empty);
        return Regex.Replace(withoutTechnicalSuffix.Trim(), @"\s+", " ");
    }

    private static void PairStretchDamage(IReadOnlyList<MutableObservation> rows)
    {
        foreach (var start in rows.Where(row => row.Marker == StretchMarker.Start))
        {
            var normalizedDescription = NormalizeStretchDescription(start.Description);
            var matchingStarts = rows
                .Where(candidate => candidate.Marker == StretchMarker.Start)
                .Where(candidate => string.Equals(
                    candidate.VsaCode,
                    start.VsaCode,
                    StringComparison.OrdinalIgnoreCase))
                .Where(candidate => string.Equals(
                    NormalizeStretchDescription(candidate.Description),
                    normalizedDescription,
                    StringComparison.Ordinal))
                .ToArray();
            var candidates = rows
                .Where(end => end.Marker == StretchMarker.End)
                .Where(end => string.Equals(
                    end.VsaCode,
                    start.VsaCode,
                    StringComparison.OrdinalIgnoreCase))
                .Where(end => end.ObservationMeter > start.ObservationMeter + 0.05)
                .Where(end => string.Equals(
                    NormalizeStretchDescription(end.Description),
                    normalizedDescription,
                    StringComparison.Ordinal))
                .ToArray();

            var byOperator = candidates
                .Where(end => HasMatchingOperatorPair(start.OperatorCode, end.OperatorCode))
                .ToArray();
            var matchingOperatorStarts = matchingStarts
                .Count(candidate => string.Equals(
                    candidate.OperatorCode,
                    start.OperatorCode,
                    StringComparison.Ordinal));
            MutableObservation? end = byOperator.Length == 1
                                      && matchingOperatorStarts == 1
                ? byOperator[0]
                : start.OperatorCode is null
                  && matchingStarts.Length == 1
                  && candidates.Length == 1
                    ? candidates[0]
                    : null;
            if (end is null)
                continue;

            start.MeterEnd = end.ObservationMeter;
            start.IsStreckenschaden = true;
            end.MeterStart = start.ObservationMeter;
            end.IsStreckenschaden = true;
        }
    }

    private static bool HasMatchingOperatorPair(string? start, string? end)
        => start is { Length: > 1 }
           && end is { Length: > 1 }
           && start[0] == 'A'
           && end[0] == 'B'
           && string.Equals(start[1..], end[1..], StringComparison.Ordinal);

    private static string NormalizeStretchDescription(string value)
        => string.Join(
            ' ',
            NonWordRegex()
                .Split(StretchMarkerRegex().Replace(value, string.Empty).ToLowerInvariant())
                .Where(token => token.Length > 0));

    private static bool TryParseMeter(string raw, out double value)
        => double.TryParse(
            raw.Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);

    private enum StretchMarker
    {
        None,
        Start,
        End,
    }

    private sealed class MutableObservation
    {
        public MutableObservation(
            string vsaCode,
            string? operatorCode,
            double meter,
            TimeSpan time,
            string description)
        {
            VsaCode = vsaCode;
            OperatorCode = operatorCode;
            ObservationMeter = meter;
            ObservationTime = time;
            Description = description;
            MeterStart = meter;
            MeterEnd = meter;
        }

        public string VsaCode { get; }
        public string? OperatorCode { get; }
        public double ObservationMeter { get; }
        public TimeSpan ObservationTime { get; }
        public string Description { get; set; }
        public double MeterStart { get; set; }
        public double MeterEnd { get; set; }
        public bool IsStreckenschaden { get; set; }
        public StretchMarker Marker => ResolveMarker(Description);

        private static StretchMarker ResolveMarker(string description)
        {
            var match = StretchMarkerRegex().Match(description);
            if (!match.Success)
                return StretchMarker.None;
            return match.Groups["marker"].Value.Equals(
                "Start",
                StringComparison.OrdinalIgnoreCase)
                ? StretchMarker.Start
                : StretchMarker.End;
        }
    }
}
