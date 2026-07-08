using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Aggregiert die Positionen aller Haltungen (ProjectCostStore.ByHolding) zu EINEM
/// NPK-Leistungsverzeichnis: gleiche Position wird über alle Haltungen zu einer Zeile
/// mit Gesamtmenge zusammengezählt. ByDN-Positionen (Liner, Manschetten) werden je DN
/// getrennt, weil der Einheitspreis nach DN variiert; Fixed-Positionen (Reinigung,
/// Fräsen, TV, Einrichtung) werden über alle DN zusammengefasst.
///
/// Reine Logik ohne WPF — analog zu <see cref="CostCalculatorLogicService"/>.
/// Der NPK-Code/Chapter wird über den ItemKey im Katalog nachgeschlagen.
/// </summary>
public static class ProjectPositionAggregator
{
    private sealed class Bucket
    {
        public string NpkCode = "";
        public string Chapter = "";
        public string ItemKey = "";
        public string Text = "";
        public string Unit = "";
        public int? Dn;
        public decimal TotalQty;
        public decimal TotalNet;
        public decimal FraesenMeter; // umgerechnete Fraesen-Meter (Zusatzinfo, wenn m->h umgerechnet)
        public readonly HashSet<string> Holdings = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<decimal> UnitPrices = new();
        public readonly HashSet<string> PriceHints = new(StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<AggregatedPosition> Aggregate(
        IEnumerable<HoldingCost>? holdings,
        IReadOnlyDictionary<string, CostCatalogItem>? catalog)
    {
        var buckets = new Dictionary<string, Bucket>(StringComparer.OrdinalIgnoreCase);

        foreach (var holding in holdings ?? Enumerable.Empty<HoldingCost>())
        {
            if (holding?.Measures is null)
                continue;

            var holdingName = string.IsNullOrWhiteSpace(holding.Holding) ? "?" : holding.Holding.Trim();

            foreach (var measure in holding.Measures)
            {
                if (measure?.Lines is null)
                    continue;

                foreach (var line in measure.Lines)
                {
                    if (!line.Selected || line.Qty <= 0m)
                        continue;

                    CostCatalogItem? item = null;
                    if (catalog is not null && !string.IsNullOrWhiteSpace(line.ItemKey))
                        catalog.TryGetValue(line.ItemKey.Trim(), out item);

                    var npk = (item?.NpkCode ?? "").Trim();
                    var chapter = (item?.Chapter ?? "").Trim();
                    var isByDn = string.Equals(item?.Type, "ByDN", StringComparison.OrdinalIgnoreCase);
                    int? dn = isByDn ? measure.Dn : null;

                    // Fraesen wird oberflaechlich in Metern erfasst, im LV aber NPK-konform in
                    // Stunden ausgewiesen (geldwert-erhaltend): h = Meter * Meterpreis / Stundenpreis.
                    // Der Preis der zugehoerigen Stundenposition (Kanalroboter) gilt fuer die
                    // Umrechnung; die Meterzahl wird als Zusatz behalten.
                    var reportUnit = (line.Unit ?? "").Trim();
                    var reportQty = line.Qty;
                    var reportUnitPrice = line.UnitPrice;
                    var fraesenMeter = 0m;
                    if (MeterToHourReport.TryGetValue((line.ItemKey ?? "").Trim(), out var hourKey)
                        && catalog is not null && catalog.TryGetValue(hourKey, out var hourItem)
                        && (hourItem.Price ?? 0m) > 0m)
                    {
                        var hourPrice = hourItem.Price!.Value;
                        fraesenMeter = line.Qty;
                        reportQty = line.Qty * line.UnitPrice / hourPrice; // Stunden (Geldbetrag bleibt gleich)
                        reportUnitPrice = hourPrice;
                        reportUnit = string.IsNullOrWhiteSpace(hourItem.Unit) ? "h" : hourItem.Unit.Trim();
                    }
                    var unit = reportUnit;

                    // Kanonischer ItemKey: fachlich gleiche Positionen im LV zu EINER Zeile buendeln.
                    // Das Fraesen-Haekchen (VORARBEIT_FRAESEN) und der eigenstaendige Kanalroboter
                    // (HAUPTARBEIT_HINDERNISSE_ROBOTER) sind dieselbe NPK-135-Position 311 (Gruppenstunden).
                    var canonicalKey = CanonicalItemKey(line.ItemKey);
                    var wasCanonicalized = !string.Equals(
                        canonicalKey, (line.ItemKey ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

                    var text = string.IsNullOrWhiteSpace(line.Text)
                        ? (line.ItemKey ?? "").Trim()
                        : line.Text.Trim();
                    // Bei Zusammenfuehrung den Namen der kanonischen Katalog-Position verwenden (konsistent).
                    if (wasCanonicalized && catalog is not null
                        && catalog.TryGetValue(canonicalKey, out var canonItem)
                        && !string.IsNullOrWhiteSpace(canonItem.Name))
                        text = canonItem.Name.Trim();

                    // Identitaet: ItemKey ist die echte Leistung (z.B. Nadelfilz vs GFK), darum
                    // PRIMAER. Mehrere App-Positionen teilen sich dieselbe NPK-Nummer (z.B. Nadelfilz,
                    // Open-End und GFK alle 612.110) - wuerde man nur nach NpkCode buendeln, verschmelzen
                    // fachlich verschiedene Leistungen in eine LV-Zeile. NpkCode bleibt nur Anzeige/Kapitel.
                    // DN nur bei ByDN-Positionen Teil des Schluessels.
                    var idPart = canonicalKey.Length > 0 ? "KEY:" + canonicalKey
                        : npk.Length > 0 ? "NPK:" + npk
                        : "TXT:" + text;
                    var key = $"{idPart}|{unit}|DN:{(dn?.ToString() ?? "-")}";

                    if (!buckets.TryGetValue(key, out var bucket))
                    {
                        bucket = new Bucket
                        {
                            NpkCode = npk,
                            Chapter = chapter,
                            ItemKey = canonicalKey,
                            Text = text,
                            Unit = unit,
                            Dn = dn
                        };
                        buckets[key] = bucket;
                    }

                    bucket.TotalQty += reportQty;
                    bucket.TotalNet += reportQty * reportUnitPrice; // = Menge*EP, Geldbetrag erhalten
                    bucket.FraesenMeter += fraesenMeter;
                    bucket.Holdings.Add(holdingName);
                    bucket.UnitPrices.Add(reportUnitPrice);
                    if (!string.IsNullOrWhiteSpace(line.PriceHint))
                        bucket.PriceHints.Add(line.PriceHint.Trim());
                }
            }
        }

        return buckets.Values
            .OrderBy(b => ChapterOrder(b.Chapter))
            .ThenBy(b => b.NpkCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(b => b.Text, StringComparer.OrdinalIgnoreCase)
            .ThenBy(b => b.Dn ?? 0)
            .Select(b =>
            {
                var distinctPrices = b.UnitPrices.Where(p => p > 0m).Distinct().ToList();
                var variable = distinctPrices.Count > 1;
                decimal? unitPrice = distinctPrices.Count == 1 ? distinctPrices[0] : null;
                var priceHint = string.Join("; ", b.PriceHints.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                // Umgerechnete Fraesen-Meter als Zusatz in der Positionsbezeichnung behalten.
                var text = b.FraesenMeter > 0m
                    ? $"{b.Text} (inkl. ca. {b.FraesenMeter:N0} m Fräsen umgerechnet)"
                    : b.Text;
                return new AggregatedPosition(
                    b.NpkCode, b.Chapter, b.ItemKey, text, b.Unit, b.Dn,
                    b.TotalQty, b.TotalNet, b.Holdings.Count, variable, unitPrice, priceHint);
            })
            .ToList();
    }

    /// <summary>NPK-Kapitel numerisch sortieren (100,200,...); Unbekanntes ans Ende.</summary>
    public static int ChapterOrder(string? chapter)
        => int.TryParse((chapter ?? "").Trim(), out var n) ? n : int.MaxValue;

    // Fachlich gleiche Positionen, die im LV zu EINER Zeile gehoeren (kanonischer ItemKey).
    private static readonly Dictionary<string, string> CanonicalItemKeyMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Fraesen-Haekchen = eigenstaendiger Kanalroboter = NPK 311 (Gruppenstunden).
            ["VORARBEIT_FRAESEN"] = "HAUPTARBEIT_HINDERNISSE_ROBOTER",
        };

    // Positionen, die oberflaechlich in Metern erfasst, im LV aber in Stunden ausgewiesen werden.
    // Wert = die stundenbasierte Position, deren Preis die Meter->Stunden-Umrechnung bestimmt.
    private static readonly Dictionary<string, string> MeterToHourReport =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["VORARBEIT_FRAESEN"] = "HAUPTARBEIT_HINDERNISSE_ROBOTER",
        };

    /// <summary>Bildet fachlich gleiche ItemKeys auf einen kanonischen Schluessel ab (sonst unveraendert).</summary>
    public static string CanonicalItemKey(string? itemKey)
    {
        var k = (itemKey ?? "").Trim();
        return CanonicalItemKeyMap.TryGetValue(k, out var canonical) ? canonical : k;
    }
}
