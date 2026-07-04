using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Baut aus den aggregierten Positionen ein NPK-Leistungsverzeichnis als CSV
/// (Semikolon-getrennt, de-CH-Zahlen mit Dezimal-PUNKT — Schweizer Excel erwartet
/// den Punkt, sonst kommen Betraege als Text an; Audit K5). Gruppiert nach
/// NPK-Kapitel mit Zwischentotalen und Gesamttotal. Die EP-Spalte bleibt leer, wo
/// der Preis variabel ist (mehrere DN/Haltungen) — dort trägt der Anwender den
/// Fixwert ein. Zwischentotale werden aus den GERUNDETEN Zeilen-Totalen gebildet,
/// damit Ausdruck und Nachrechnung auf den Rappen uebereinstimmen (Audit W15).
/// </summary>
public static class NpkLeistungsverzeichnisExporter
{
    private static readonly NumberFormatInfo Nf = new()
    {
        NumberDecimalSeparator = ".",
        NumberGroupSeparator = ""
    };

    public static string BuildCsv(
        IReadOnlyList<AggregatedPosition> positions,
        string currency = "CHF",
        decimal excludedPauschaleTotal = 0m,
        int excludedPauschaleHoldingCount = 0)
    {
        var cur = string.IsNullOrWhiteSpace(currency) ? "CHF" : currency.Trim();
        var sb = new StringBuilder();
        // Hinweis: BOM/Encoding macht die Datei-Schicht (UTF8Encoding mit BOM),
        // damit der reine CSV-String testbar bleibt.
        sb.AppendLine($"NPK;Position;DN;Menge;Einheit;EP {cur};Total {cur};Haltungen");
        foreach (var warning in BuildDuplicateNpkUnitWarnings(positions))
            sb.AppendLine($";WARNUNG: {Csv(warning)};;;;;;");

        var grandTotal = 0m;

        foreach (var chapterGroup in (positions ?? new List<AggregatedPosition>())
                     .GroupBy(p => p.Chapter ?? "")
                     .OrderBy(g => ProjectPositionAggregator.ChapterOrder(g.Key)))
        {
            sb.AppendLine($";{Csv(ChapterTitle(chapterGroup.Key))};;;;;;");

            var chapterTotal = 0m;
            foreach (var p in chapterGroup)
            {
                var ep = p.UnitPrice.HasValue ? p.UnitPrice.Value.ToString("0.00", Nf) : "";
                // Zeilen-Total auf Rappen festziehen und NUR die gerundeten Werte summieren —
                // sonst weicht das gedruckte Zwischentotal von den gedruckten Zeilen ab (W15).
                var lineTotal = Math.Round(p.TotalNet, 2, MidpointRounding.AwayFromZero);
                var total = lineTotal.ToString("0.00", Nf);
                chapterTotal += lineTotal;
                var text = AppendPriceHint(p.Text, p.PriceHint);

                sb.Append(CsvText(p.NpkCode)).Append(';')
                    .Append(Csv(text)).Append(';')
                    .Append(p.Dn?.ToString(CultureInfo.InvariantCulture) ?? "").Append(';')
                    .Append(p.TotalQty.ToString("0.###", Nf)).Append(';')
                    .Append(Csv(p.Unit)).Append(';')
                    .Append(ep).Append(';')
                    .Append(total).Append(';')
                    .Append(p.HoldingCount.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            grandTotal += chapterTotal;
            sb.AppendLine($";Zwischentotal {Csv(ChapterTitle(chapterGroup.Key))};;;;;{chapterTotal.ToString("0.00", Nf)};");
        }

        sb.AppendLine($";TOTAL (exkl. MwSt.);;;;;{grandTotal.ToString("0.00", Nf)};");
        if (excludedPauschaleTotal > 0m)
        {
            var countText = excludedPauschaleHoldingCount > 0
                ? $" ({excludedPauschaleHoldingCount} Haltung(en))"
                : "";
            sb.AppendLine($";Nicht enthaltene Pauschalkosten{countText};;;;;{excludedPauschaleTotal.ToString("0.00", Nf)};");
        }

        return sb.ToString();
    }

    public static string ChapterTitle(string? chapter) => (chapter ?? "").Trim() switch
    {
        "100" => "NPK 100 — Einrichtung",
        "112" => "NPK 112 — Prüfungen (Dichtheit)",
        "200" => "NPK 200 — Reinigung / Zustandserfassung",
        "300" => "NPK 300 — Vorarbeiten",
        "400" => "NPK 400 — Wasserhaltung",
        "500" => "NPK 500 — Reparatur",
        "600" => "NPK 600 — Renovierung",
        "700" => "NPK 700 — Schächte / Bauwerke",
        "900" => "NPK 900 — Abschluss",
        "" => "Übrige Positionen (ohne NPK-Kapitel)",
        _ => $"NPK {chapter}"
    };

    private static string Csv(string? value)
    {
        var v = value ?? "";
        if (v.IndexOf(';') >= 0 || v.IndexOf('"') >= 0 || v.IndexOf('\n') >= 0 || v.IndexOf('\r') >= 0)
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    private static string AppendPriceHint(string? text, string? priceHint)
    {
        var t = (text ?? "").Trim();
        var h = (priceHint ?? "").Trim();
        if (h.Length == 0)
            return t;
        if (t.Length == 0)
            return h;
        return t.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0
            ? t
            : $"{t} ({h})";
    }

    /// <summary>
    /// Excel-feste Text-Zelle: NPK-Nummern wie "612.110" wuerden von Excel als Zahl
    /// gelesen (612.11 bzw. 612110 je nach Locale). Das ="..."-Muster erzwingt Text (Audit K4).
    /// </summary>
    private static string CsvText(string? value)
        => string.IsNullOrEmpty(value) ? "" : "=\"" + value.Replace("\"", "\"\"") + "\"";

    private static IEnumerable<string> BuildDuplicateNpkUnitWarnings(IReadOnlyList<AggregatedPosition>? positions)
    {
        return (positions ?? new List<AggregatedPosition>())
            .Where(p => !string.IsNullOrWhiteSpace(p.NpkCode))
            .GroupBy(p => p.NpkCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Code = g.Key,
                Units = g.Select(p => (p.Unit ?? "").Trim())
                    .Where(u => u.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .Where(x => x.Units.Count > 1)
            .Select(x => $"NPK {x.Code} kommt mit unterschiedlichen Einheiten vor: {string.Join(", ", x.Units)}");
    }
}
