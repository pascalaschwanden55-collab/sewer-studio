using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

internal static class ProtocolPdfObservationText
{
    private static readonly Regex RawMeterRegex =
        new(@"@?\s*(\d+(?:[.,]\d+)?)\s*m(?!m)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RawTimeRegex =
        new(@"\b(\d{1,2}:\d{2}(?::\d{2})?)\b", RegexOptions.Compiled);

    internal static string BuildRangeText(ProtocolEntry entry, string rangeLabel)
    {
        if (entry.MeterStart is null && entry.MeterEnd is null)
            return $"{rangeLabel} -";

        var m1 = FmtMeterValue(entry.MeterStart);
        var m2 = FmtMeterValue(entry.MeterEnd);
        var prefix = IsEstimatedMeter(entry) ? "ca. " : "";
        return $"{rangeLabel} {prefix}{m1}-{m2} m";
    }

    internal static string BuildDetailLine(ProtocolEntry entry)
    {
        var parts = new List<string>();
        if (entry.Zeit.HasValue)
            parts.Add("Zeit " + FormatTime(entry.Zeit.Value));
        if (!string.IsNullOrWhiteSpace(entry.Mpeg))
            parts.Add("MPEG " + entry.Mpeg.Trim());
        return string.Join(" | ", parts);
    }

    internal static string BuildObservationMeterText(ProtocolEntry entry)
    {
        var start = entry.MeterStart;
        var end = entry.MeterEnd;
        var prefix = IsEstimatedMeter(entry) ? "ca. " : "";

        if (entry.IsStreckenschaden && start.HasValue && end.HasValue)
            return $"{prefix}{FmtMeterValue(start)}\u2013{FmtMeterValue(end)}";

        if (start.HasValue)
            return $"{prefix}{FmtMeterValue(start)}";

        if (end.HasValue)
            return $"{prefix}{FmtMeterValue(end)}";

        return "-";
    }

    internal static string BuildObservationTimeText(ProtocolEntry entry)
    {
        var parts = new List<string>();
        if (entry.Zeit.HasValue)
            parts.Add(FormatTime(entry.Zeit.Value));
        if (!string.IsNullOrWhiteSpace(entry.Mpeg))
            parts.Add("MPEG " + entry.Mpeg.Trim());
        return string.Join(" | ", parts);
    }

    internal static string BuildObservationMeterStartText(ProtocolEntry entry)
    {
        var value = entry.MeterStart ?? entry.MeterEnd;
        if (!value.HasValue)
            return "-";

        var prefix = IsEstimatedMeter(entry) ? "ca. " : "";
        return $"{prefix}{FmtMeterValue(value)}";
    }

    internal static string BuildObservationMpegText(ProtocolEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Mpeg))
            return entry.Mpeg.Trim();
        if (entry.Zeit.HasValue)
            return FormatTime(entry.Zeit.Value);
        return "-";
    }

    internal static string BuildObservationPhotoText(ProtocolEntry entry)
    {
        if (entry.FotoPaths is null || entry.FotoPaths.Count == 0)
            return "-";
        return entry.FotoPaths.Count.ToString(CultureInfo.InvariantCulture);
    }

    internal static string BuildObservationStufeText(ProtocolEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.CodeMeta?.Severity))
            return entry.CodeMeta.Severity!.Trim();
        if (entry.CodeMeta?.Count is not null)
            return entry.CodeMeta.Count.Value.ToString(CultureInfo.InvariantCulture);
        return "-";
    }

    internal static string BuildPhotoTitle(ProtocolEntry entry)
    {
        var code = string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim();
        var description = entry.Beschreibung?.Trim();
        if (!string.IsNullOrWhiteSpace(description))
            return $"{code} \u2013 {description}";
        return code;
    }

    internal static string BuildPhotoMeta(ProtocolEntry entry)
    {
        var parts = new List<string>();
        var meter = BuildObservationMeterText(entry);
        if (!string.IsNullOrWhiteSpace(meter) && meter != "-")
        {
            var label = entry.IsStreckenschaden ? "Strecke" : "Meter";
            parts.Add($"{label} {meter} m");
        }

        var time = BuildObservationTimeText(entry);
        if (!string.IsNullOrWhiteSpace(time))
            parts.Add(time);

        return string.Join(" | ", parts);
    }

    internal static string BuildPhotoCaptionLine1(ProtocolEntry entry, int index)
    {
        var line = $"{index}.";
        var time = BuildPhotoTimeText(entry);
        var meter = BuildObservationMeterStartText(entry);

        if (!string.IsNullOrWhiteSpace(time))
            line += $" {time}";
        if (!string.IsNullOrWhiteSpace(meter) && meter != "-")
            line += string.IsNullOrWhiteSpace(time) ? $" {meter} m" : $", {meter} m";

        return line.Trim();
    }

    internal static string BuildPhotoCaptionLine2(ProtocolEntry entry)
    {
        var code = string.IsNullOrWhiteSpace(entry.Code) ? "" : entry.Code.Trim();
        var desc = entry.Beschreibung?.Trim();
        if (string.IsNullOrWhiteSpace(desc))
            desc = BuildParameterShortText(entry);
        if (string.IsNullOrWhiteSpace(desc))
            desc = entry.CodeMeta?.Notes?.Trim();

        if (!string.IsNullOrWhiteSpace(desc))
            desc = Shorten(desc, 90);

        if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(desc))
            return $"{code} {desc}";
        if (!string.IsNullOrWhiteSpace(code))
            return code;
        return desc ?? string.Empty;
    }

    internal static string BuildPhotoTimeText(ProtocolEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Mpeg))
            return entry.Mpeg.Trim();
        if (entry.Zeit.HasValue)
            return FormatTime(entry.Zeit.Value);
        return string.Empty;
    }

    internal static string BuildObservationNotesText(ProtocolEntry entry)
    {
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is not null)
        {
            var remark = GetParam(parameters, "vsa.anmerkung");
            if (!string.IsNullOrWhiteSpace(remark))
                return Shorten(remark.Trim(), 60);
        }

        if (!string.IsNullOrWhiteSpace(entry.CodeMeta?.Notes))
            return Shorten(entry.CodeMeta.Notes.Trim(), 60);

        return "-";
    }

    internal static string BuildParameterShortText(ProtocolEntry entry)
    {
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is null || parameters.Count == 0)
            return string.Empty;

        var list = new List<string>();
        foreach (var kv in parameters.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(kv.Value))
                continue;
            if (kv.Key.StartsWith("catalog.", StringComparison.OrdinalIgnoreCase))
                continue;
            if (kv.Key.StartsWith("vsa.", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(kv.Key, "Quantifizierung1", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(kv.Key, "Quantifizierung2", StringComparison.OrdinalIgnoreCase))
                continue;

            list.Add($"{kv.Key}={kv.Value}");
        }

        if (list.Count == 0)
        {
            var q1 = GetParam(parameters, "Quantifizierung1") ?? GetParam(parameters, "vsa.q1");
            var q2 = GetParam(parameters, "Quantifizierung2") ?? GetParam(parameters, "vsa.q2");
            if (!string.IsNullOrWhiteSpace(q1))
                list.Add($"Q1={q1}");
            if (!string.IsNullOrWhiteSpace(q2))
                list.Add($"Q2={q2}");
        }

        return string.Join(", ", list);
    }

    internal static string BuildVsaParameterLine(ProtocolEntry entry)
    {
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is null || parameters.Count == 0)
            return string.Empty;

        var parts = new List<string>();

        var q1 = GetParam(parameters, "vsa.q1") ?? GetParam(parameters, "Quantifizierung1");
        var q2 = GetParam(parameters, "vsa.q2") ?? GetParam(parameters, "Quantifizierung2");
        if (!string.IsNullOrWhiteSpace(q1)) parts.Add($"Q1={q1}");
        if (!string.IsNullOrWhiteSpace(q2)) parts.Add($"Q2={q2}");

        var distanz = GetParam(parameters, "vsa.distanz");
        if (!string.IsNullOrWhiteSpace(distanz)) parts.Add($"Distanz={distanz}");

        var uhrVon = GetParam(parameters, "vsa.uhr.von");
        var uhrBis = GetParam(parameters, "vsa.uhr.bis");
        if (!string.IsNullOrWhiteSpace(uhrVon) || !string.IsNullOrWhiteSpace(uhrBis))
            parts.Add($"Uhr {uhrVon ?? "-"}-{uhrBis ?? "-"}");

        var strecke = GetParam(parameters, "vsa.strecke");
        if (!string.IsNullOrWhiteSpace(strecke)) parts.Add($"Strecke={strecke}");

        var verbindung = GetParam(parameters, "vsa.verbindung");
        if (IsTruthy(verbindung)) parts.Add("Verbindung=Ja");

        var ansicht = GetParam(parameters, "vsa.ansicht");
        if (!string.IsNullOrWhiteSpace(ansicht)) parts.Add($"Ansicht={ansicht}");

        var ez = GetParam(parameters, "vsa.ez");
        if (!string.IsNullOrWhiteSpace(ez)) parts.Add($"EZ={ez}");

        var schacht = GetParam(parameters, "vsa.schachtbereich");
        if (!string.IsNullOrWhiteSpace(schacht)) parts.Add($"Schachtbereich={schacht}");

        var anmerkung = GetParam(parameters, "vsa.anmerkung");
        if (!string.IsNullOrWhiteSpace(anmerkung)) parts.Add($"Diverses={anmerkung}");

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Quantifizierung1",
            "Quantifizierung2",
            "vsa.q1",
            "vsa.q2",
            "vsa.distanz",
            "vsa.uhr.von",
            "vsa.uhr.bis",
            "vsa.strecke",
            "vsa.verbindung",
            "vsa.ansicht",
            "vsa.ez",
            "vsa.schachtbereich",
            "vsa.anmerkung"
        };

        foreach (var kv in parameters
                     .Where(kv => !known.Contains(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                     .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            parts.Add($"{kv.Key}={kv.Value}");
        }

        if (!string.IsNullOrWhiteSpace(entry.CodeMeta?.Severity))
            parts.Add($"Severity={entry.CodeMeta.Severity}");
        if (entry.CodeMeta?.Count is not null)
            parts.Add($"Count={entry.CodeMeta.Count}");
        if (!string.IsNullOrWhiteSpace(entry.CodeMeta?.Notes))
            parts.Add($"Notiz={entry.CodeMeta.Notes}");

        return parts.Count == 0 ? string.Empty : "Parameter: " + string.Join(" | ", parts);
    }

    internal static string Shorten(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        if (text.Length <= max)
            return text;
        return text.Substring(0, Math.Max(0, max - 1)).TrimEnd() + "\u2026";
    }

    internal static string FmtMeterValue(double? value)
        => value is null ? "-" : value.Value.ToString("0.00", CultureInfo.InvariantCulture);

    internal static string FormatTime(TimeSpan value)
        => value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");

    internal static TimeSpan? ParseMpegTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
            return parsed;

        return null;
    }

    internal static double? TryParseDouble(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    internal static double? TryParseMeterFromRaw(string raw)
    {
        var match = RawMeterRegex.Match(raw);
        if (!match.Success)
            return null;

        var text = match.Groups[1].Value.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    internal static double? TryParseSecondMeterFromRaw(string raw)
    {
        var matches = RawMeterRegex.Matches(raw);
        if (matches.Count < 2)
            return null;

        var text = matches[1].Groups[1].Value.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    internal static string? TryParseTimeFromRaw(string raw)
    {
        var match = RawTimeRegex.Match(raw);
        return match.Success ? match.Groups[1].Value : null;
    }

    internal static string? GetParam(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value) ? value : null;

    internal static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return string.Equals(value, "ja", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEstimatedMeter(ProtocolEntry entry)
        => entry.Ai?.IsMeterEstimated == true
           || string.Equals(entry.Ai?.MeterSource, "LinearEstimate", StringComparison.OrdinalIgnoreCase);
}
