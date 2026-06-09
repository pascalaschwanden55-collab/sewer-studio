using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Baut aus den aggregierten Positionen ein NPK-Leistungsverzeichnis als CSV
/// (Semikolon-getrennt, de-CH-Zahlen). Gruppiert nach NPK-Kapitel mit
/// Zwischentotalen und Gesamttotal. Die EP-Spalte bleibt leer, wo der Preis
/// variabel ist (mehrere DN/Haltungen) — dort trägt der Anwender den Fixwert ein.
/// </summary>
public static class NpkLeistungsverzeichnisExporter
{
    private static readonly NumberFormatInfo Nf = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ""
    };

    public static string BuildCsv(IReadOnlyList<AggregatedPosition> positions, string currency = "CHF")
    {
        var cur = string.IsNullOrWhiteSpace(currency) ? "CHF" : currency.Trim();
        var sb = new StringBuilder();
        // Hinweis: BOM/Encoding macht die Datei-Schicht (UTF8Encoding mit BOM),
        // damit der reine CSV-String testbar bleibt.
        sb.AppendLine($"NPK;Position;DN;Menge;Einheit;EP {cur};Total {cur};Haltungen");

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
                var total = p.TotalNet.ToString("0.00", Nf);
                chapterTotal += p.TotalNet;

                sb.Append(Csv(p.NpkCode)).Append(';')
                    .Append(Csv(p.Text)).Append(';')
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
        return sb.ToString();
    }

    public static string ChapterTitle(string? chapter) => (chapter ?? "").Trim() switch
    {
        "100" => "NPK 100 — Einrichtung",
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
}
