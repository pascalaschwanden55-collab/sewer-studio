using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Vsa;

public static class ConnectionCountEstimator
{
    private enum ConnectionCodeRole
    {
        None,
        Main,        // BCA* (Hauptcode Anschluss)
        Supplemental // BAG*/BAH* etc. (Zusatzcode zum Anschluss)
    }

    private static readonly Regex NumberPattern = new(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);
    private static readonly Regex DistPattern = new(
        @"(?:@\s*|bei\s+|km\s*)?(?<m>\d{1,4}(?:[.,]\d{1,2})?)\s*m(?!m)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ClockPattern = new(@"(?<clock>\d{1,2})\s*(?:Uhr|h)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CodePattern = new(@"[^A-Z0-9]+", RegexOptions.Compiled);
    private static readonly Regex SpacePattern = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex LeadingCodeRegex = new(@"^(?<code>[A-Z]{3,6})\b[\s:;,-]*", RegexOptions.Compiled);
    private static readonly string[] ConnectionKeywords =
    {
        "anschluss",
        "anschl",
        "seiteneinlauf",
        "sattelanschluss",
        "stutzen"
    };

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

        var exactLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var meterOnlyLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fallbackKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in findings)
        {
            if (!IsConnectionEntry(finding.KanalSchadencode, finding.Raw))
                continue;

            var distance = finding.SchadenlageAnfang ?? TryExtractDistance(finding.Raw);
            var clock = NormalizeClock(TryExtractClock(finding.Raw));
            var code = NormalizeCode(finding.KanalSchadencode);
            var role = GetConnectionCodeRole(code);
            var fallback = BuildFallbackKey(finding.Raw, code, role);

            TryRegisterConnection(
                exactLocations,
                meterOnlyLocations,
                fallbackKeys,
                distance,
                clock,
                fallback);
        }

        return exactLocations.Count + meterOnlyLocations.Count + fallbackKeys.Count;
    }

    private static int EstimateFromPrimaryDamages(string? primaryDamages)
    {
        if (string.IsNullOrWhiteSpace(primaryDamages))
            return 0;

        var exactLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var meterOnlyLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fallbackKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
            var clock = NormalizeClock(TryExtractClock(line));
            var role = GetConnectionCodeRole(code);
            var fallback = BuildFallbackKey(line, code, role);

            TryRegisterConnection(
                exactLocations,
                meterOnlyLocations,
                fallbackKeys,
                distance,
                clock,
                fallback);
        }

        return exactLocations.Count + meterOnlyLocations.Count + fallbackKeys.Count;
    }

    private static void TryRegisterConnection(
        HashSet<string> exactLocations,
        HashSet<string> meterOnlyLocations,
        HashSet<string> fallbackKeys,
        double? distance,
        string? clock,
        string? fallbackKey)
    {
        if (distance is null)
        {
            if (!string.IsNullOrWhiteSpace(fallbackKey))
                fallbackKeys.Add(fallbackKey);
            return;
        }

        var meter = BuildMeterToken(distance.Value);
        if (string.IsNullOrWhiteSpace(clock))
        {
            // Unknown clock means "one Anschluss at this meter". This collapses
            // duplicates across Untercodes (e.g. BCAEA + BAHC) to one connection.
            if (exactLocations.Any(x => x.StartsWith(meter + "|", StringComparison.OrdinalIgnoreCase)))
                return;
            meterOnlyLocations.Add(meter);
            return;
        }

        if (meterOnlyLocations.Contains(meter))
            return;

        exactLocations.Add($"{meter}|U:{clock}");
    }

    private static string BuildMeterToken(double distance)
    {
        var rounded = Math.Round(distance, 1, MidpointRounding.AwayFromZero);
        return $"M:{rounded.ToString("0.0", CultureInfo.InvariantCulture)}";
    }

    private static string? NormalizeClock(string? rawClock)
    {
        if (string.IsNullOrWhiteSpace(rawClock))
            return null;

        var text = rawClock.Trim();
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
            return text;

        if (hour <= 0)
            return null;

        hour = ((hour - 1) % 12) + 1;
        return hour.ToString("00", CultureInfo.InvariantCulture);
    }

    private static string? BuildFallbackKey(string? raw, string code, ConnectionCodeRole role)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var normalized = NormalizeWhitespace(raw);
            // "BCAEA Anschluss..." und "BAHC Anschluss..." am selben Ort
            // sollen im fallback nicht als zwei verschiedene Schluessel gelten.
            normalized = StripLeadingCode(normalized);
            return "R:" + normalized;
        }
        if (!string.IsNullOrWhiteSpace(code))
            return "C:" + code;
        return null;
    }

    private static bool IsConnectionEntry(string? codeRaw, string? detailRaw)
    {
        var detail = NormalizeWhitespace(detailRaw).ToLowerInvariant();
        if (ConnectionKeywords.Any(k => detail.Contains(k, StringComparison.Ordinal)))
            return true;

        return GetConnectionCodeRole(codeRaw) != ConnectionCodeRole.None;
    }

    private static ConnectionCodeRole GetConnectionCodeRole(string? codeRaw)
    {
        var code = NormalizeCode(codeRaw);
        if (string.IsNullOrWhiteSpace(code))
            return ConnectionCodeRole.None;

        // VSA-Hauptcode fuer Anschluss
        if (code.StartsWith("BCA", StringComparison.OrdinalIgnoreCase))
            return ConnectionCodeRole.Main;

        // VSA-Zusatzcodes fuer Anschluss-Zustand
        // BAF = Einragender Anschluss (Oberflaechenschaden am Anschluss)
        if (code.StartsWith("BAF", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("BAG", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("BAH", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionCodeRole.Supplemental;
        }

        // Legacy/Fallback-Kodierung
        if (code.StartsWith("BGA", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("BGB", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("ANSCHL", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectionCodeRole.Supplemental;
        }

        return ConnectionCodeRole.None;
    }

    private static string StripLeadingCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var match = LeadingCodeRegex.Match(text);
        if (!match.Success)
            return text;

        var code = match.Groups["code"].Value;
        var role = GetConnectionCodeRole(code);
        if (role == ConnectionCodeRole.None)
            return text;

        var stripped = text[match.Length..].Trim();
        return stripped.Length == 0 ? text : stripped;
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

        var rounded = (int)Math.Round(parsed, 0, MidpointRounding.AwayFromZero);
        if (rounded > 999)
            return null;

        return rounded;
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
