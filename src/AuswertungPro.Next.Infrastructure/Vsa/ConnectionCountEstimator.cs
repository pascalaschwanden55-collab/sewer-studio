using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Vsa;

public static class ConnectionCountEstimator
{
    private static readonly Regex NumberPattern = new(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);
    private static readonly Regex DistPattern = new(@"@\s*(?<m>\d{1,4}(?:[.,]\d{1,2})?)\s*m", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClockPattern = new(@"(?<clock>\d{1,2})\s*Uhr", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CodePattern = new(@"[^A-Z0-9]+", RegexOptions.Compiled);
    private static readonly Regex SpacePattern = new(@"\s+", RegexOptions.Compiled);

    public static int? EstimateFromRecord(HaltungRecord? record)
    {
        if (record is null)
            return null;

        var explicitCount = TryParseConnectionCount(record.GetFieldValue("Anschluesse_verpressen"));
        if (explicitCount is not null)
            return explicitCount;

        var fromFindings = EstimateFromFindings(record.VsaFindings);
        if (fromFindings > 0)
            return fromFindings;

        var fromPrimaryDamages = EstimateFromPrimaryDamages(record.GetFieldValue("Primaere_Schaeden"));
        return fromPrimaryDamages > 0 ? fromPrimaryDamages : null;
    }

    private static int EstimateFromFindings(IEnumerable<VsaFinding>? findings)
    {
        if (findings is null)
            return 0;

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in findings)
        {
            if (!IsConnectionEntry(finding.KanalSchadencode, finding.Raw))
                continue;

            var key = BuildFindingKey(finding);
            if (!string.IsNullOrWhiteSpace(key))
                unique.Add(key);
        }

        return unique.Count;
    }

    private static int EstimateFromPrimaryDamages(string? primaryDamages)
    {
        if (string.IsNullOrWhiteSpace(primaryDamages))
            return 0;

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = primaryDamages.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var firstToken = line
                .Split(new[] { ' ', '\t', '@', '(', ')', ':', ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            var code = NormalizeCode(firstToken);

            if (!IsConnectionEntry(code, line))
                continue;

            var distance = TryExtractDistance(line);
            var clock = TryExtractClock(line);
            if (distance is not null)
            {
                unique.Add(BuildLocationKey(distance.Value, clock));
            }
            else
            {
                unique.Add("R:" + NormalizeWhitespace(line));
            }
        }

        return unique.Count;
    }

    private static string BuildFindingKey(VsaFinding finding)
    {
        var distance = finding.SchadenlageAnfang ?? TryExtractDistance(finding.Raw);
        var clock = TryExtractClock(finding.Raw);

        if (distance is not null)
            return BuildLocationKey(distance.Value, clock);

        if (!string.IsNullOrWhiteSpace(finding.Raw))
            return "R:" + NormalizeWhitespace(finding.Raw!);

        var code = NormalizeCode(finding.KanalSchadencode);
        if (!string.IsNullOrWhiteSpace(code))
            return "C:" + code;

        return string.Empty;
    }

    private static string BuildLocationKey(double distance, string? clock)
    {
        var rounded = Math.Round(distance, 1, MidpointRounding.AwayFromZero);
        var clockToken = string.IsNullOrWhiteSpace(clock) ? "-" : clock;
        return $"M:{rounded:0.0}|U:{clockToken}";
    }

    private static bool IsConnectionEntry(string? codeRaw, string? detailRaw)
    {
        var detail = NormalizeWhitespace(detailRaw).ToLowerInvariant();
        if (detail.Contains("anschl", StringComparison.Ordinal))
            return true;

        var code = NormalizeCode(codeRaw);
        if (string.IsNullOrWhiteSpace(code))
            return false;

        if (code.StartsWith("BCA", StringComparison.OrdinalIgnoreCase))
            return true;
        if (code.StartsWith("BAH", StringComparison.OrdinalIgnoreCase))
            return true;
        if (code.Contains("ANSCHL", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static int? TryParseConnectionCount(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
            return null;

        var numberMatch = NumberPattern.Match(text);
        if (!numberMatch.Success)
            return null;

        var normalized = numberMatch.Value.Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return null;

        if (parsed < 0)
            return null;

        return (int)Math.Round(parsed, 0, MidpointRounding.AwayFromZero);
    }

    private static double? TryExtractDistance(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = DistPattern.Match(text);
        if (!match.Success)
            return null;

        var raw = match.Groups["m"].Value.Replace(',', '.');
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return null;
    }

    private static string? TryExtractClock(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = ClockPattern.Match(text);
        if (!match.Success)
            return null;

        return match.Groups["clock"].Value;
    }

    private static string NormalizeCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var upper = raw.Trim().ToUpperInvariant();
        return CodePattern.Replace(upper, string.Empty);
    }

    private static string NormalizeWhitespace(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        return SpacePattern.Replace(raw.Trim(), " ");
    }
}
