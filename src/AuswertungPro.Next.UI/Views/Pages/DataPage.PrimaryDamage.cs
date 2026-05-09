using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Pages;

// DataPage Primaerschaden-Preview: Aufbereitung der Primaer-Schaden-Liste
// fuer den Tooltip-/Detail-Bereich. Liest VsaFindings + freien Raw-Text,
// dedupliziert nach (Meter|Code), formatiert pro Zeile mit Code-Titel,
// Quantifizierung und freiem Text. Aus dem Hauptdatei extrahiert (Slice 6c).
public partial class DataPage
{
    private string BuildPrimaryDamagePreviewContent(HaltungRecord record)
    {
        // Phase 5.1.B Etappe 3.H: via DI-Container.
        AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider? catalog = null;
        try { catalog = App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>(); } catch { }

        // Audit-Fix 2026-04: Beide Quellen verbinden (UNION statt Exklusiv-Fallback).
        var fromFindings = BuildPrimaryDamageLinesFromFindings(record, catalog);
        var fromRaw = BuildPrimaryDamageLinesFromRaw(record.GetFieldValue("Primaere_Schaeden"), catalog);

        // Deduplizieren ueber (Meter+Code)-Praefix der Zeile.
        // Format: "0.00m BCD ..." -> Schluessel "0.00|BCD"
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<string>();

        static string KeyOf(string line)
        {
            var trimmed = line.Trim();
            // Erste 2 Tokens (Meter + Code) als Schluessel
            var parts = trimmed.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? $"{parts[0]}|{parts[1]}" : trimmed;
        }

        // VsaFindings haben Vorrang, weil sie Quantifizierung/Title strukturierter haben
        foreach (var line in fromFindings)
        {
            if (seenKeys.Add(KeyOf(line)))
                merged.Add(line);
        }
        // Raw-Lines die nicht via Code+Meter abgedeckt sind ergaenzen
        foreach (var line in fromRaw)
        {
            if (seenKeys.Add(KeyOf(line)))
                merged.Add(line);
        }

        if (merged.Count == 0)
            return record.GetFieldValue("Primaere_Schaeden") ?? string.Empty;

        // Nach Meter sortieren (numerisch) damit die Reihenfolge stabil ist
        merged.Sort((a, b) => CompareByMeter(a, b));
        return string.Join("\n", merged);
    }

    /// <summary>Sortiert Preview-Zeilen nach dem fuehrenden Meter-Wert (z.B. "0.79m ...").</summary>
    private static int CompareByMeter(string a, string b)
    {
        static double? Meter(string line)
        {
            var trimmed = line.Trim();
            var space = trimmed.IndexOf(' ');
            if (space <= 0) return null;
            var first = trimmed.Substring(0, space).TrimEnd('m', 'M');
            return double.TryParse(first, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        }
        var ma = Meter(a);
        var mb = Meter(b);
        if (ma.HasValue && mb.HasValue) return ma.Value.CompareTo(mb.Value);
        if (ma.HasValue) return -1;
        if (mb.HasValue) return 1;
        return string.CompareOrdinal(a, b);
    }

    private static List<string> BuildPrimaryDamageLinesFromFindings(HaltungRecord record, AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider? sp)
    {
        var lines = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (record.VsaFindings is null || record.VsaFindings.Count == 0)
            return lines;

        foreach (var finding in record.VsaFindings.Where(f => !string.IsNullOrWhiteSpace(f.KanalSchadencode)))
        {
            var code = NormalizePrimaryCode(finding.KanalSchadencode);
            if (!IsLikelyPrimaryCode(code))
                continue;

            var meter = finding.MeterStart ?? finding.SchadenlageAnfang ?? TryExtractMeter(finding.Raw);
            var dedupeKey = meter.HasValue
                ? $"{code}|{meter.Value.ToString("F2", CultureInfo.InvariantCulture)}"
                : $"{code}|";
            if (!seen.Add(dedupeKey))
                continue;

            var title = TryResolveCodeTitle(sp, code);
            var q1 = FirstNonEmpty(finding.Quantifizierung1, TryExtractQuantification(finding.Raw, PrimaryQuant1Regex));
            var q2 = FirstNonEmpty(finding.Quantifizierung2, TryExtractQuantification(finding.Raw, PrimaryQuant2Regex));
            var text = ExtractFreeText(finding.Raw, code, title);
            var formatted = FormatPrimaryPreviewLine(meter, code, title, text, q1, q2);
            if (!string.IsNullOrWhiteSpace(formatted))
                lines.Add(formatted);
        }

        return lines;
    }

    private static List<string> BuildPrimaryDamageLinesFromRaw(string? rawText, AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider? sp)
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

            var code = TryExtractPrimaryCode(line);
            if (!IsLikelyPrimaryCode(code))
            {
                lines.Add(line);
                continue;
            }

            var meter = TryExtractMeter(line);
            var title = TryResolveCodeTitle(sp, code);
            var q1 = TryExtractQuantification(line, PrimaryQuant1Regex);
            var q2 = TryExtractQuantification(line, PrimaryQuant2Regex);
            var text = ExtractFreeText(line, code, title);
            var formatted = FormatPrimaryPreviewLine(meter, code, title, text, q1, q2);
            lines.Add(string.IsNullOrWhiteSpace(formatted) ? line : formatted);
        }

        return lines;
    }

    private static string FormatPrimaryPreviewLine(
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

    private static string TryExtractPrimaryCode(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return string.Empty;

        var withoutLeadingMeter = PrimaryMeterLeadingRegex.Replace(line.Trim(), "").Trim();
        var separators = new[] { ' ', '\t', '@', '(', ')', ':', ';', ',', '|' };
        var token = withoutLeadingMeter.Split(separators, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var code = NormalizePrimaryCode(token);
        if (IsLikelyPrimaryCode(code))
            return code;

        var atIndex = withoutLeadingMeter.IndexOf('@');
        if (atIndex > 0)
        {
            var beforeAt = withoutLeadingMeter.Substring(0, atIndex).Trim();
            token = beforeAt.Split(separators, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            code = NormalizePrimaryCode(token);
            if (IsLikelyPrimaryCode(code))
                return code;
        }

        return string.Empty;
    }

    private static bool IsLikelyPrimaryCode(string code)
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

    private static string NormalizePrimaryCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        return Regex.Replace(raw.Trim().ToUpperInvariant(), @"[^A-Z0-9]", "");
    }

    private static double? TryExtractMeter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var leading = PrimaryMeterLeadingRegex.Match(raw);
        if (leading.Success && TryParseDoubleInvariant(leading.Groups["m"].Value, out var leadingMeter))
            return leadingMeter;

        var at = PrimaryMeterAtRegex.Match(raw);
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
        text = PrimaryMeterLeadingRegex.Replace(text, "").Trim();
        text = PrimaryMeterAtRegex.Replace(text, "").Trim();
        text = PrimaryQuantStripRegex.Replace(text, "").Trim();
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

    private static string? TryResolveCodeTitle(AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider? catalog, string code)
    {
        if (catalog is null || string.IsNullOrWhiteSpace(code))
            return null;
        if (!catalog.TryGet(code, out var def))
            return null;
        return string.IsNullOrWhiteSpace(def.Title) ? null : def.Title.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
