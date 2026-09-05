using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Pure-Static-Parser fuer Massnahmen, Schadenscodes und Kostenwerte aus HaltungRecord.
/// Enthaelt ausschliesslich zustandslose Hilfsmethoden ohne IO, Threading oder externe Abhaengigkeiten.
/// </summary>
internal static class MeasureRecordParser
{
    // ── Typen ────────────────────────────────────────────────────────

    /// <summary>
    /// Kostenmomentaufnahme eines Haltungs-Datensatzes.
    /// </summary>
    internal readonly record struct CostSnapshot(
        decimal? TotalCost,
        decimal? InlinerMeters,
        int? InlinerStk,
        int? AnschluesseVerpressen,
        int? ReparaturManschette,
        int? ReparaturKurzliner);

    // ── Normalisierung ───────────────────────────────────────────────

    /// <summary>
    /// Normalisiert einen VSA-Schadensode auf Grossbuchstaben;
    /// gibt leer zurueck bei ungueltigem Format oder reservierten Woertern.
    /// </summary>
    internal static string NormalizeCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim().ToUpperInvariant();

        // VSA-Codes bestehen aus Buchstaben. Ziffern duerfen nicht still entfernt
        // werden: Sonst wird z.B. "0.00m" zum erfundenen Code "000M" und ein
        // Operator-Code wie "A01" landet ebenfalls in den Lerndaten.
        if (Regex.IsMatch(text, @"[0-9_]"))
            return string.Empty;

        text = Regex.Replace(text, @"[^A-Z]", "");

        if (text.Length < 2 || text.Length > 8)
            return string.Empty;
        if (text is "SCHADEN" or "SCHAEDEN" or "KEINE")
            return string.Empty;

        return text;
    }

    /// <summary>
    /// Nur bauliche Schaeden (BA...) und betriebliche Schaeden (BB...) sind
    /// eine belastbare Grundlage fuer eine Sanierungsmassnahme. BC beschreibt
    /// Bestandesmerkmale wie Anschluss, Bogen, Rohranfang/-ende; BD ist eine
    /// allgemeine Zustandsangabe. Beides darf das Massnahmenmodell nicht lernen.
    /// </summary>
    internal static bool IsMeasureRelevantDamageCode(string? value)
    {
        var code = NormalizeCode(value);
        return code.Length >= 3
            && (code.StartsWith("BA", StringComparison.Ordinal)
                || code.StartsWith("BB", StringComparison.Ordinal));
    }

    /// <summary>
    /// Normalisiert einen Massnahmen-Text: trimmen und fuehrende Aufzaehlungszeichen entfernen.
    /// </summary>
    internal static string NormalizeMeasure(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        while (text.Length > 0 && (text[0] == '-' || text[0] == '*'))
            text = text[1..].TrimStart();
        return text;
    }

    // ── Parser ───────────────────────────────────────────────────────

    /// <summary>
    /// Zerlegt einen rohen Massnahmentext in eine sortierte, deduplizierte Liste normalisierter Massnahmen.
    /// </summary>
    internal static List<string> ParseMeasures(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw.Split(new[] { '\r', '\n', ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeMeasure)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Extrahiert normalisierte Schadenscodes aus VsaFindings und Primaere_Schaeden-Feld des Records.
    /// </summary>
    internal static List<string> ExtractDamageCodes(HaltungRecord record)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in record.VsaFindings)
        {
            var code = NormalizeCode(finding.KanalSchadencode);
            if (IsMeasureRelevantDamageCode(code))
                result.Add(code);
        }

        var primary = record.GetFieldValue("Primaere_Schaeden");
        if (!string.IsNullOrWhiteSpace(primary))
        {
            var lines = primary.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parsed = PrimaryDamageLineParser.ParsePrimaryDamageLine(line);
                var rawCode = parsed?.Code;

                // Alte Projekte enthalten vereinzelt Zeilen, die nur aus dem Code
                // bestehen. Diese bleiben unterstuetzt, ohne wieder den ersten
                // beliebigen Token (z.B. eine Meterangabe) zu akzeptieren.
                if (rawCode is null && Regex.IsMatch(
                        line.Trim(),
                        @"^[A-Z]{2,6}(?:\.[A-Z]{1,2})?$",
                        RegexOptions.IgnoreCase))
                {
                    rawCode = line.Trim();
                }

                var code = NormalizeCode(rawCode);
                if (IsMeasureRelevantDamageCode(code))
                    result.Add(code);
            }
        }

        return result
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ── Zahlen-Parser ────────────────────────────────────────────────

    /// <summary>
    /// Parst einen rohen Dezimalwert; Komma wird als Dezimaltrennzeichen akzeptiert.
    /// </summary>
    internal static decimal? TryParseDecimal(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return null;
        text = text.Replace(",", ".");
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Parst einen rohen Ganzzahlwert; Dezimalzahlen werden kaufmaennisch gerundet.
    /// </summary>
    internal static int? TryParseInt(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
            return intValue;

        text = text.Replace(",", ".");
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var decimalValue))
            return (int)Math.Round(decimalValue, 0, MidpointRounding.AwayFromZero);

        return null;
    }

    // ── Signaturen ───────────────────────────────────────────────────

    /// <summary>
    /// Erzeugt eine sortierte, semikolongetrennte Code-Signatur fuer Aggregationszwecke.
    /// </summary>
    internal static string BuildCodeSignature(IReadOnlyList<string> codes)
        => string.Join(";", codes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Erzeugt eine eindeutige Sample-Signatur aus Record-ID, Codes, Massnahmen und Kosten.
    /// </summary>
    internal static string BuildSampleSignature(Guid recordId, IReadOnlyList<string> codes, IReadOnlyList<string> measures, CostSnapshot costs)
    {
        var total = costs.TotalCost?.ToString("0.00", CultureInfo.InvariantCulture) ?? "";
        var inlinerM = costs.InlinerMeters?.ToString("0.00", CultureInfo.InvariantCulture) ?? "";
        var inlinerStk = costs.InlinerStk?.ToString(CultureInfo.InvariantCulture) ?? "";
        var anschluesse = costs.AnschluesseVerpressen?.ToString(CultureInfo.InvariantCulture) ?? "";
        var manschette = costs.ReparaturManschette?.ToString(CultureInfo.InvariantCulture) ?? "";
        var kurzliner = costs.ReparaturKurzliner?.ToString(CultureInfo.InvariantCulture) ?? "";
        return $"{recordId:N}|{string.Join(";", codes)}|{string.Join(";", measures)}|{total}|{inlinerM}|{inlinerStk}|{anschluesse}|{manschette}|{kurzliner}";
    }

    // ── Berechnungen ─────────────────────────────────────────────────

    /// <summary>
    /// Berechnet den Durchschnitt eines Decimal-Summenwertes; gibt null zurueck bei count &lt;= 0.
    /// </summary>
    internal static decimal? AverageDecimal(decimal sum, int count, int decimals)
    {
        if (count <= 0)
            return null;
        return Math.Round(sum / count, decimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Berechnet den kaufmaennisch gerundeten Integer-Durchschnitt; gibt null zurueck bei count &lt;= 0.
    /// </summary>
    internal static int? AverageInt(int sum, int count)
    {
        if (count <= 0)
            return null;
        return (int)Math.Round((decimal)sum / count, 0, MidpointRounding.AwayFromZero);
    }

    // ── Validierung ──────────────────────────────────────────────────

    /// <summary>
    /// Bereinigt Kostenwerte: setzt unplausible Werte (negativ, null oder ueber Schwellenwert) auf null.
    /// </summary>
    internal static CostSnapshot SanitizeCosts(CostSnapshot costs)
    {
        return new CostSnapshot(
            costs.TotalCost is > 0 and <= 10_000_000m ? costs.TotalCost : null,
            costs.InlinerMeters is > 0 and <= 100_000m ? costs.InlinerMeters : null,
            costs.InlinerStk is > 0 and <= 10_000 ? costs.InlinerStk : null,
            costs.AnschluesseVerpressen is > 0 and <= 10_000 ? costs.AnschluesseVerpressen : null,
            costs.ReparaturManschette is > 0 and <= 10_000 ? costs.ReparaturManschette : null,
            costs.ReparaturKurzliner is > 0 and <= 10_000 ? costs.ReparaturKurzliner : null);
    }
}
