using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

internal static class HoldingIdPlausibility
{
    private const int MaxNormalizedLength = 32;
    private const int MaxDigitsPerSide = 16;
    private const int MaxRepeatedDigitRun = 5;

    private static readonly Regex BasicHoldingIdRegex = new(
        @"^\d[\d\.]*-\d[\d\.]*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex RepeatedDigitRunRegex = new(
        @"(\d)\1{" + MaxRepeatedDigitRun + @",}",
        RegexOptions.CultureInvariant);

    private static readonly Regex[] LabeledCandidateRegexes =
    {
        new(@"(?im)\bHaltungsname\s*:\s*(?<id>\d[\d\.]*\s*[-/]\s*\d[\d\.]*)\b", RegexOptions.CultureInvariant),
        new(@"(?im)\bHaltungs(?:nummer|name)?\b[^\n]{0,120}?(?<id>\d[\d\.]*\s*[-/]\s*\d[\d\.]*)\b", RegexOptions.CultureInvariant),
        new(@"(?im)^\s*(?<id>\d[\d\.]*\s*[-/]\s*\d[\d\.]*)\s+\d{2}\.\d{2}\.\d{4}\b", RegexOptions.CultureInvariant)
    };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Trim();
        normalized = Regex.Replace(normalized, @"\s+", "");
        normalized = normalized.Replace('/', '-');
        normalized = normalized.Replace("\u2013", "-");
        normalized = normalized.Replace("\u2014", "-");
        return normalized;
    }

    public static bool IsLikelyHoldingId(string? value)
    {
        return !TryGetImplausibilityReason(value, out _, out _);
    }

    public static bool TryGetImplausibilityReason(string? value, out string normalized, out string reason)
    {
        normalized = Normalize(value);
        reason = "";

        if (string.IsNullOrWhiteSpace(normalized))
        {
            reason = "leer";
            return true;
        }

        if (!BasicHoldingIdRegex.IsMatch(normalized))
        {
            reason = "kein Haltungsnummer-Format";
            return true;
        }

        if (normalized.Length > MaxNormalizedLength)
        {
            reason = $"zu lang ({normalized.Length} > {MaxNormalizedLength})";
            return true;
        }

        var parts = normalized.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            reason = "nicht genau zwei Nummernteile";
            return true;
        }

        foreach (var part in parts)
        {
            var digits = part.Count(char.IsDigit);
            if (digits > MaxDigitsPerSide)
            {
                reason = $"Nummernteil zu lang ({digits} Ziffern > {MaxDigitsPerSide})";
                return true;
            }
        }

        if (RepeatedDigitRunRegex.IsMatch(normalized))
        {
            reason = $"lange Wiederholungs-Ziffernfolge (>{MaxRepeatedDigitRun})";
            return true;
        }

        return false;
    }

    public static string? FindFirstImplausibleLabeledCandidate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        foreach (var rx in LabeledCandidateRegexes)
        {
            var match = rx.Match(text);
            if (!match.Success)
                continue;

            var raw = match.Groups["id"].Value;
            if (TryGetImplausibilityReason(raw, out var normalized, out _))
                return normalized;
        }

        return null;
    }
}
