using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Wandelt das vom PDF-Parser erzeugte Feld "Primaere_Schaeden" in strukturierte
/// VSA-Befunde um. A-/B-Marker mit gleicher Nummer werden zu einem Streckenschaden
/// verbunden.
/// </summary>
internal static class PdfPrimaryDamageFindingBuilder
{
    private static readonly Regex RangeLineRegex = new(
        @"^(?<marker>[AB]\d{2})\s+(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})*)\s+@(?<meter>\d+(?:[.,]\d+)?)\s*m?\s*(?:\((?<desc>.*)\))?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RangeSuffixRegex = new(
        @"(?:,\s*)?(?:Start|Beginn|Ende)(?:\s+\d+)?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static List<VsaFinding> Build(string? primaryDamages)
    {
        var findings = new List<VsaFinding>();
        if (string.IsNullOrWhiteSpace(primaryDamages))
            return findings;

        var openRanges = new Dictionary<string, VsaFinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in primaryDamages.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (TryParseRangeLine(line, out var marker, out var code, out var meter, out var description))
            {
                var rangeKey = $"{marker[1..]}|{code}";
                if (marker.StartsWith('A'))
                {
                    var finding = CreateFinding(code, meter, CleanRangeDescription(description));
                    findings.Add(finding);
                    openRanges[rangeKey] = finding;
                }
                else if (openRanges.Remove(rangeKey, out var startFinding))
                {
                    startFinding.MeterEnd = meter;
                    if (string.IsNullOrWhiteSpace(startFinding.Raw))
                        startFinding.Raw = CleanRangeDescription(description);
                }
                else
                {
                    findings.Add(CreateFinding(code, meter, CleanRangeDescription(description)));
                }

                continue;
            }

            var parsed = PrimaryDamageLineParser.ParsePrimaryDamageLine(line);
            if (parsed is null)
                continue;

            findings.Add(CreateFinding(
                parsed.Value.Code,
                parsed.Value.Meter,
                parsed.Value.Description));
        }

        return findings;
    }

    private static bool TryParseRangeLine(
        string line,
        out string marker,
        out string code,
        out double meter,
        out string description)
    {
        marker = string.Empty;
        code = string.Empty;
        meter = 0;
        description = string.Empty;

        var match = RangeLineRegex.Match(line);
        if (!match.Success)
            return false;

        marker = match.Groups["marker"].Value.ToUpperInvariant();
        code = match.Groups["code"].Value.ToUpperInvariant();
        meter = PrimaryDamageLineParser.TryParseMeterValue(match.Groups["meter"].Value);
        description = match.Groups["desc"].Value.Trim();
        return true;
    }

    private static VsaFinding CreateFinding(string code, double meter, string? description)
        => new()
        {
            KanalSchadencode = code.Trim().ToUpperInvariant(),
            MeterStart = meter,
            Raw = string.IsNullOrWhiteSpace(description) ? code.Trim().ToUpperInvariant() : description.Trim()
        };

    private static string CleanRangeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        return RangeSuffixRegex.Replace(description.Trim(), string.Empty).Trim().TrimEnd(',');
    }
}
