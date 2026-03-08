using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

public static class XtfPrimaryDamageFormatter
{
    private static readonly Regex NonCodeCharsRegex = new(@"[^A-Z0-9]+", RegexOptions.Compiled);
    private static readonly Regex SpaceRegex = new(@"\s+", RegexOptions.Compiled);

    // WinCan-interne GUID-Fragmente: "c06c5c-c9", "6ec06c5c-c9a3-4b12" etc.
    private static readonly Regex GuidFragmentRegex = new(@"[0-9a-f]{2,8}(?:-[0-9a-f]{2,8})+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // WinCan Section-Key-Referenzen: "-80631_6e"
    private static readonly Regex SectionKeyRefRegex = new(@"-\d+_[0-9a-f]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Lazy<IReadOnlyDictionary<string, string>> CodeTitles = new(LoadCodeTitles);

    private static readonly IReadOnlyDictionary<string, string> FallbackTitles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BCA"] = "Anschluss",
            ["BCB"] = "Punktuelle Reparatur",
            ["BCC"] = "Krummung der Leitung",
            ["BCD"] = "Rohranfang",
            ["BCE"] = "Einlauf in Leitung",
            ["BDA"] = "Allgemeinzustand, Fotobeispiel",
            ["BDB"] = "Allgemeine Anmerkung",
            ["BDC"] = "Abbruch der Inspektion"
        };

    public static string FormatLine(VsaFinding finding)
    {
        var code = NormalizeCode(finding.KanalSchadencode);
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        var parts = new List<string>();
        var meter = finding.MeterStart ?? finding.SchadenlageAnfang;
        if (meter.HasValue)
            parts.Add($"{meter.Value:0.00}m");

        parts.Add(code);

        var title = ResolveCodeTitle(code);
        if (!string.IsNullOrWhiteSpace(title))
            parts.Add(title!);

        var raw = NormalizeRaw(finding.Raw, code, title);
        if (!string.IsNullOrWhiteSpace(raw))
            parts.Add($"({raw})");

        var q1 = NormalizeQuantification(finding.Quantifizierung1);
        var q2 = NormalizeQuantification(finding.Quantifizierung2);
        if (!string.IsNullOrWhiteSpace(q1))
            parts.Add($"Q1={q1}");
        if (!string.IsNullOrWhiteSpace(q2))
            parts.Add($"Q2={q2}");

        return string.Join(" ", parts);
    }

    public static string FormatLines(IReadOnlyList<VsaFinding> findings)
    {
        if (findings.Count == 0)
            return string.Empty;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>(findings.Count);
        foreach (var finding in findings)
        {
            var code = NormalizeCode(finding.KanalSchadencode);
            if (code.Length == 0)
                continue;

            var meter = finding.MeterStart ?? finding.SchadenlageAnfang;
            var key = $"{code}|{(meter.HasValue ? meter.Value.ToString("F2") : "")}";
            if (!seen.Add(key))
                continue;

            var line = FormatLine(finding);
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line);
        }

        return lines.Count == 0
            ? string.Empty
            : string.Join("\n", lines);
    }

    private static string NormalizeCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var upper = raw.Trim().ToUpperInvariant();
        return NonCodeCharsRegex.Replace(upper, string.Empty);
    }

    private static string? ResolveCodeTitle(string code)
    {
        if (CodeTitles.Value.TryGetValue(code, out var title) && !string.IsNullOrWhiteSpace(title))
            return title;
        return null;
    }

    private static string? NormalizeRaw(string? raw, string code, string? title)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = SpaceRegex.Replace(raw.Replace("\r\n", " ").Replace('\n', ' ').Trim(), " ");
        if (text.Length == 0)
            return null;

        // WinCan-interne IDs und GUID-Fragmente entfernen
        text = SectionKeyRefRegex.Replace(text, "");
        text = GuidFragmentRegex.Replace(text, "");
        text = SpaceRegex.Replace(text.Trim(), " ").Trim();

        if (text.Length == 0)
            return null;

        if (string.Equals(text, code, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.IsNullOrWhiteSpace(title) && string.Equals(text, title, StringComparison.OrdinalIgnoreCase))
            return null;

        return text;
    }

    private static string? NormalizeQuantification(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        return text.Length == 0 ? null : text;
    }

    // Streckenschaden-Marker: A01, A02, B01, B02, ... (DIN EN 13508-2)
    private static readonly Regex ContinuousDefectMarkerRegex = new(@"^[AB]\d{2}$", RegexOptions.Compiled);
    // VSA-Code am Anfang eines Tokens: 3-5 Grossbuchstaben
    private static readonly Regex EmbeddedVsaCodeRegex = new(@"^([A-Z]{3,5})\b", RegexOptions.Compiled);
    // Erste Zeichen einer Zeile: optionaler Meter + Code
    private static readonly Regex LineCodeRegex = new(@"^\s*(?:\d+[.,]\d+\s*m?\s+)?([A-Z0-9]{2,6})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // Meter irgendwo in der Zeile (erste Zahl gefolgt von optionalem 'm')
    private static readonly Regex LineMeterRegex = new(@"(\d+[.,]\d+)\s*m?\b", RegexOptions.Compiled);

    /// <summary>
    /// Dedupliziert einen fertigen Primaere_Schaeden-Text zeilenweise.
    /// Erkennt Streckenschaden-Marker (A01, B02) und loest sie zum echten VSA-Code auf.
    /// Gleicher effektiver Code am gleichen Meter = Duplikat → wird entfernt.
    /// Entfernt WinCan-interne GUID-Fragmente und Section-Key-Referenzen aus Klammern.
    /// </summary>
    public static string DeduplicateText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        var rawLines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (rawLines.Length <= 1)
            return CleanGuidFragments(text);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(rawLines.Length);

        foreach (var rawLine in rawLines)
        {
            var line = CleanGuidFragments(rawLine.Trim());
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var code = ExtractCodeFromLine(line);
            var meter = ExtractMeterFromLine(line);

            // Streckenschaden-Marker zum echten VSA-Code aufloesen
            var effectiveCode = ResolveMarkerCode(code, line);

            var key = effectiveCode.Length > 0
                ? $"{effectiveCode}|{meter}"
                : line; // Fallback: ganze Zeile als Key

            if (!seen.Add(key))
                continue;

            result.Add(line);
        }

        return string.Join("\n", result);
    }

    private static string ExtractCodeFromLine(string line)
    {
        var match = LineCodeRegex.Match(line);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : string.Empty;
    }

    private static string ExtractMeterFromLine(string line)
    {
        var match = LineMeterRegex.Match(line);
        if (!match.Success) return string.Empty;
        if (double.TryParse(match.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var val))
            return val.ToString("F2");
        return match.Groups[1].Value;
    }

    private static string ResolveMarkerCode(string code, string fullLine)
    {
        if (code.Length == 0 || !ContinuousDefectMarkerRegex.IsMatch(code))
            return code;

        // Versuche den echten VSA-Code nach dem Marker zu finden
        // Typisch: "0.00m A01 (BBCC (Harte Ablagerungen...))" oder "0.00m A01 BBCC ..."
        var afterCode = fullLine;
        var codeIdx = fullLine.IndexOf(code, StringComparison.OrdinalIgnoreCase);
        if (codeIdx >= 0)
            afterCode = fullLine.Substring(codeIdx + code.Length).TrimStart(' ', '(');

        var match = EmbeddedVsaCodeRegex.Match(afterCode);
        return match.Success ? match.Groups[1].Value : code;
    }

    // Klammer mit GUID/Section-Key-Inhalt: "(Rohranfang -80631_6e c06c5c-c9)"
    private static readonly Regex ParenGuidRegex = new(
        @"\(([^)]*?(?:" +
        @"-\d+_[0-9a-f]+" +    // Section-Key: -80631_6e
        @"|[0-9a-f]{2,8}(?:-[0-9a-f]{2,8})+" + // GUID-Fragment: c06c5c-c9
        @")[^)]*)\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Entfernt WinCan-interne GUID-Fragmente und Section-Key-Referenzen aus einer Zeile.
    /// "(Rohranfang -80631_6e c06c5c-c9)" → "(Rohranfang)" oder komplett entfernt wenn leer.
    /// </summary>
    private static string CleanGuidFragments(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return line;

        // Klammern mit GUID-Fragmenten bereinigen
        var cleaned = ParenGuidRegex.Replace(line, m =>
        {
            var inner = m.Groups[1].Value;
            // GUID-Fragmente und Section-Keys entfernen
            inner = SectionKeyRefRegex.Replace(inner, "");
            inner = GuidFragmentRegex.Replace(inner, "");
            inner = SpaceRegex.Replace(inner.Trim(), " ").Trim();

            // Wenn nach Bereinigung noch Text uebrig ist, Klammer beibehalten
            return inner.Length > 0 ? $"({inner})" : "";
        });

        // Auch ausserhalb von Klammern bereinigen
        cleaned = SectionKeyRefRegex.Replace(cleaned, "");
        cleaned = GuidFragmentRegex.Replace(cleaned, "");

        return SpaceRegex.Replace(cleaned.Trim(), " ").Trim();
    }

    private static IReadOnlyDictionary<string, string> LoadCodeTitles()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var catalogPath = Path.Combine(AppContext.BaseDirectory, "Data", "vsa_codes.json");
            if (File.Exists(catalogPath))
            {
                var provider = new JsonCodeCatalogProvider(catalogPath);
                foreach (var codeDef in provider.GetAll())
                {
                    var code = NormalizeCode(codeDef.Code);
                    var title = (codeDef.Title ?? string.Empty).Trim();
                    if (code.Length == 0 || title.Length == 0)
                        continue;

                    map[code] = title;
                }
            }
        }
        catch
        {
            // keep fallback-only map
        }

        foreach (var kv in FallbackTitles)
        {
            if (!map.ContainsKey(kv.Key))
                map[kv.Key] = kv.Value;
        }

        return map;
    }
}
