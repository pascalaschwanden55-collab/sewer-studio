using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Reads date and shaft number values from interactive shaft PDF form fields.
/// Keeping these form-specific rules separate prevents the distributor from
/// accumulating another PDF format implementation.
/// </summary>
internal static class ShaftPdfFormFieldParser
{
    private static readonly Regex DateRegex = new(
        @"\b(?<d>" + SewerTextPatterns.GermanDateCore + @"|\d{4}[./-]\d{2}[./-]\d{2})\b",
        RegexOptions.Compiled);

    internal static string BuildSyntheticText(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        var lines = new List<string>(entries.Count * 2);
        foreach (var entry in entries)
        {
            var labels = new[] { entry.PartialName, entry.AlternateName, entry.MappingName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (labels.Count == 0)
            {
                lines.Add(entry.Value);
                continue;
            }

            foreach (var label in labels)
                lines.Add($"{label}: {entry.Value}");
        }

        return string.Join("\n", lines);
    }

    internal static DateTime? TryExtractDate(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        // A labeled date is more reliable than an arbitrary date-like value.
        foreach (var entry in entries)
        {
            if (!ContainsDateLabel(BuildLabel(entry)))
                continue;

            var parsed = TryParseDate(entry.Value);
            if (parsed is not null)
                return parsed;
        }

        // Some forms use generic internal field names.
        foreach (var entry in entries)
        {
            var parsed = TryParseDate(entry.Value);
            if (parsed is not null)
                return parsed;
        }

        return null;
    }

    internal static string? TryExtractShaftNumber(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        // A labeled shaft number is more reliable than an arbitrary number.
        foreach (var entry in entries)
        {
            if (!ContainsShaftNumberLabel(BuildLabel(entry)))
                continue;

            var candidate = ExtractShaftNumberToken(entry.Value);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        // Some forms use generic internal field names.
        foreach (var entry in entries)
        {
            var candidate = ExtractShaftNumberToken(entry.Value);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }

    private static DateTime? TryParseDate(string value)
    {
        var match = DateRegex.Match(value);
        return match.Success
               && HoldingTextNormalizer.TryParseDateString(match.Groups["d"].Value, out var parsed)
            ? parsed
            : null;
    }

    private static string BuildLabel(PdfFormFieldEntry entry)
        => string.Join(" ",
            new[] { entry.PartialName, entry.AlternateName, entry.MappingName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));

    private static bool ContainsDateLabel(string? label)
        => !string.IsNullOrWhiteSpace(label)
           && (label.Contains("datum", StringComparison.OrdinalIgnoreCase)
               || label.Contains("date", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsShaftNumberLabel(string? label)
        => !string.IsNullOrWhiteSpace(label)
           && (label.Contains("schacht", StringComparison.OrdinalIgnoreCase)
               || label.Contains("nummer", StringComparison.OrdinalIgnoreCase)
               || Regex.IsMatch(label, @"\bnr\.?\b", RegexOptions.IgnoreCase));

    private static string? ExtractShaftNumberToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var direct = Regex.Match(value.Trim(), @"^(?<nr>\d{3,8})$");
        if (direct.Success)
            return direct.Groups["nr"].Value;

        var any = Regex.Match(value, @"\b(?<nr>\d{3,8})\b");
        if (!any.Success)
            return null;

        var token = any.Groups["nr"].Value;
        if (token.Length == 4 && int.TryParse(token, out var year) && year >= 1900 && year <= 2100)
            return null;

        return token;
    }
}
