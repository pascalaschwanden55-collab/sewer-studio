using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Reports;

// ProtocolPdfExporter Format-Helpers: Pure Formatter und Parser fuer Werte
// (GetMeta, NormalizeValue, FmtMeterValue, FormatTime, ParseMpegTime,
// TryParseDouble, TryParseMeterFromRaw/SecondMeterFromRaw/TimeFromRaw,
// GetParam, IsTruthy, MapToLine, Svg, EscapeCsv, FmtMeter).
// Aus dem Hauptdatei extrahiert (Slice 36).
public sealed partial class ProtocolPdfExporter
{
    private static string? GetMeta(Project project, string key)
        => project.Metadata.TryGetValue(key, out var v) ? v : null;

    private static string NormalizeValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static List<(string Label, string? Value)> FilterNonEmpty(List<(string Label, string? Value)> items)
        => items.Where(i => !string.IsNullOrWhiteSpace(i.Value)).ToList();

    private static string FmtMeterValue(double? value)
        => value is null ? "-" : value.Value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatTime(TimeSpan value)
        => value.TotalHours >= 1 ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");

    private static TimeSpan? ParseMpegTime(string? raw)
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

    private static double? TryParseDouble(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var normalized = raw.Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static readonly Regex RawMeterRegex =
        new(@"@?\s*(\d+(?:[.,]\d+)?)\s*m(?!m)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RawTimeRegex =
        new(@"\b(\d{1,2}:\d{2}(?::\d{2})?)\b", RegexOptions.Compiled);

    private static double? TryParseMeterFromRaw(string raw)
    {
        var match = RawMeterRegex.Match(raw);
        if (!match.Success)
            return null;

        var text = match.Groups[1].Value.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static double? TryParseSecondMeterFromRaw(string raw)
    {
        var matches = RawMeterRegex.Matches(raw);
        if (matches.Count < 2)
            return null;

        var text = matches[1].Groups[1].Value.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string? TryParseTimeFromRaw(string raw)
    {
        var match = RawTimeRegex.Match(raw);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? GetParam(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var value) ? value : null;

    private static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        return string.Equals(value, "ja", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static double MapToLine(double value, double length, double left, double right)
    {
        if (length <= 0)
            return left;
        var t = Math.Clamp(value / length, 0d, 1d);
        return left + (right - left) * t;
    }

    private static string Svg(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string EscapeCsv(string s)
    {
        if (s.Contains('"') || s.Contains(';') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static string FmtMeter(double? m) => m is null ? "—" : m.Value.ToString("0.00");
}
