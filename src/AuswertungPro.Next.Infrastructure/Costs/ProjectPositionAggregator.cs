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
        public readonly HashSet<string> Holdings = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<decimal> UnitPrices = new();
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

                    var text = string.IsNullOrWhiteSpace(line.Text)
                        ? (line.ItemKey ?? "").Trim()
                        : line.Text.Trim();
                    var unit = (line.Unit ?? "").Trim();

                    // Identitaet: NPK-Code wenn vorhanden, sonst ItemKey, sonst Text.
                    // DN nur bei ByDN-Positionen Teil des Schluessels.
                    var idPart = npk.Length > 0 ? "NPK:" + npk
                        : !string.IsNullOrWhiteSpace(line.ItemKey) ? "KEY:" + line.ItemKey.Trim()
                        : "TXT:" + text;
                    var key = $"{idPart}|{unit}|DN:{(dn?.ToString() ?? "-")}";

                    if (!buckets.TryGetValue(key, out var bucket))
                    {
                        bucket = new Bucket
                        {
                            NpkCode = npk,
                            Chapter = chapter,
                            ItemKey = (line.ItemKey ?? "").Trim(),
                            Text = text,
                            Unit = unit,
                            Dn = dn
                        };
                        buckets[key] = bucket;
                    }

                    bucket.TotalQty += line.Qty;
                    bucket.TotalNet += line.Qty * line.UnitPrice;
                    bucket.Holdings.Add(holdingName);
                    bucket.UnitPrices.Add(line.UnitPrice);
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
                return new AggregatedPosition(
                    b.NpkCode, b.Chapter, b.ItemKey, b.Text, b.Unit, b.Dn,
                    b.TotalQty, b.TotalNet, b.Holdings.Count, variable, unitPrice);
            })
            .ToList();
    }

    /// <summary>NPK-Kapitel numerisch sortieren (100,200,...); Unbekanntes ans Ende.</summary>
    public static int ChapterOrder(string? chapter)
        => int.TryParse((chapter ?? "").Trim(), out var n) ? n : int.MaxValue;
}
