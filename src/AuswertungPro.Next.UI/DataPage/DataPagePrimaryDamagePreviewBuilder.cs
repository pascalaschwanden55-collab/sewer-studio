using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPagePrimaryDamagePreviewBuilder
{
    private static readonly Regex MeterAtRegex = new(
        @"@\s*(?<m>\d+(?:[.,]\d+)?)\s*m\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MeterLeadingRegex = new(
        @"^\s*(?<m>\d+(?:[.,]\d+)?)\s*m\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Quant1Regex = new(
        @"\b(?:Q1|Quantifizierung1)\s*[:=]\s*(?<v>[^\s;,|)]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Quant2Regex = new(
        @"\b(?:Q2|Quantifizierung2)\s*[:=]\s*(?<v>[^\s;,|)]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QuantStripRegex = new(
        @"\b(?:Q1|Q2|Quantifizierung1|Quantifizierung2)\s*[:=]\s*[^\s;,|)]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Build(HaltungRecord record, Func<string, string?>? resolveCodeTitle = null)
    {
        var lines = BuildLinesFromFindings(record, resolveCodeTitle);
        if (lines.Count == 0)
            lines = BuildLinesFromRaw(record.GetFieldValue("Primaere_Schaeden"), resolveCodeTitle);

        return lines.Count == 0
            ? record.GetFieldValue("Primaere_Schaeden")
            : string.Join("\n", lines);
    }

    public static List<string> BuildLinesFromFindings(HaltungRecord record, Func<string, string?>? resolveCodeTitle = null)
    {
        var lines = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (record.VsaFindings is null || record.VsaFindings.Count == 0)
            return lines;

        foreach (var finding in record.VsaFindings.Where(f => !string.IsNullOrWhiteSpace(f.KanalSchadencode)))
        {
            var code = NormalizeCode(finding.KanalSchadencode);
            if (!IsLikelyCode(code))
                continue;

            var meter = finding.MeterStart ?? finding.SchadenlageAnfang ?? TryExtractMeter(finding.Raw);
            var dedupeKey = meter.HasValue
                ? $"{code}|{meter.Value.ToString("F2", CultureInfo.InvariantCulture)}"
                : $"{code}|";
            if (!seen.Add(dedupeKey))
                continue;

            var title = ResolveCodeTitle(resolveCodeTitle, code);
            var q1 = FirstNonEmpty(finding.Quantifizierung1, TryExtractQuantification(finding.Raw, Quant1Regex));
            var q2 = FirstNonEmpty(finding.Quantifizierung2, TryExtractQuantification(finding.Raw, Quant2Regex));
            var text = ExtractFreeText(finding.Raw, code, title);
            var formatted = FormatLine(meter, code, title, text, q1, q2);
            if (!string.IsNullOrWhiteSpace(formatted))
                lines.Add(formatted);
        }

        return lines;
    }

    public static List<string> BuildLinesFromRaw(string? rawText, Func<string, string?>? resolveCodeTitle = null)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(rawText))
            return lines;

        var rawLines = rawText.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in rawLines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            var code = TryExtractCode(line);
            if (!IsLikelyCode(code))
            {
                lines.Add(line);
                continue;
            }

            var meter = TryExtractMeter(line);
            var title = ResolveCodeTitle(resolveCodeTitle, code);
            var q1 = TryExtractQuantification(line, Quant1Regex);
            var q2 = TryExtractQuantification(line, Quant2Regex);
            var text = ExtractFreeText(line, code, title);
            var formatted = FormatLine(meter, code, title, text, q1, q2);
            lines.Add(string.IsNullOrWhiteSpace(formatted) ? line : formatted);
        }

        return lines;
    }

    private static string FormatLine(
        double? meter,
        string code,
        string? title,
        string? text,
        string? q1,
        string? q2)
    {
        var parts = new List<string>();
        if (meter.HasValue)
            parts.Add($"{meter.Value:0.00}m");

        if (!string.IsNullOrWhiteSpace(code))
            parts.Add(code);
        if (!string.IsNullOrWhiteSpace(title))
            parts.Add(title!);
        if (!string.IsNullOrWhiteSpace(text))
            parts.Add($"({text})");
        if (!string.IsNullOrWhiteSpace(q1))
            parts.Add($"Q1={q1}");
        if (!string.IsNullOrWhiteSpace(q2))
            parts.Add($"Q2={q2}");

        return string.Join(" ", parts);
    }

    private static string TryExtractCode(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return string.Empty;

        var withoutLeadingMeter = MeterLeadingRegex.Replace(line.Trim(), "").Trim();
        var separators = new[] { ' ', '\t', '@', '(', ')', ':', ';', ',', '|' };
        var token = withoutLeadingMeter.Split(separators, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var code = NormalizeCode(token);
        if (IsLikelyCode(code))
            return code;

        var atIndex = withoutLeadingMeter.IndexOf('@');
        if (atIndex > 0)
        {
            var beforeAt = withoutLeadingMeter.Substring(0, atIndex).Trim();
            token = beforeAt.Split(separators, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            code = NormalizeCode(token);
            if (IsLikelyCode(code))
                return code;
        }

        return string.Empty;
    }

    private static bool IsLikelyCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
        if (code.Length < 3 || code.Length > 6)
            return false;
        if (!char.IsLetter(code[0]))
            return false;
        if (!code.Any(char.IsLetter))
            return false;
        return true;
    }

    private static string NormalizeCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        return Regex.Replace(raw.Trim().ToUpperInvariant(), @"[^A-Z0-9]", "");
    }

    private static double? TryExtractMeter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var leading = MeterLeadingRegex.Match(raw);
        if (leading.Success && TryParseDoubleInvariant(leading.Groups["m"].Value, out var leadingMeter))
            return leadingMeter;

        var at = MeterAtRegex.Match(raw);
        if (at.Success && TryParseDoubleInvariant(at.Groups["m"].Value, out var atMeter))
            return atMeter;

        return null;
    }

    private static bool TryParseDoubleInvariant(string raw, out double value)
    {
        var normalized = (raw ?? string.Empty).Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string? TryExtractQuantification(string? raw, Regex pattern)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var match = pattern.Match(raw);
        if (!match.Success)
            return null;

        var value = match.Groups["v"].Value.Trim();
        return value.Length == 0 ? null : value;
    }

    private static string? ExtractFreeText(string? raw, string code, string? title)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        if (text.Length == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(code))
            text = Regex.Replace(text, @"^\s*" + Regex.Escape(code) + @"\b", "", RegexOptions.IgnoreCase).Trim();
        text = MeterLeadingRegex.Replace(text, "").Trim();
        text = MeterAtRegex.Replace(text, "").Trim();
        text = QuantStripRegex.Replace(text, "").Trim();
        if (text.StartsWith("(") && text.EndsWith(")") && text.Length > 2)
            text = text[1..^1].Trim();
        text = Regex.Replace(text, @"\s+", " ").Trim(' ', '-', ',', ';', '|');

        if (text.Length == 0)
            return null;
        if (string.Equals(text, code, StringComparison.OrdinalIgnoreCase))
            return null;
        if (!string.IsNullOrWhiteSpace(title) && string.Equals(text, title, StringComparison.OrdinalIgnoreCase))
            return null;

        return text;
    }

    private static string? ResolveCodeTitle(Func<string, string?>? resolveCodeTitle, string code)
        => string.IsNullOrWhiteSpace(code) ? null : resolveCodeTitle?.Invoke(code)?.Trim();

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
