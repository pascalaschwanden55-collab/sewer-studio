using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Costs;

/// <summary>Eine Verfahrenszeile fuer das Cockpit: Menge, Einheit und Nettobetrag.</summary>
public sealed record RehabilitationQuantity(
    SpecialStatsCategory Category,
    string Label,
    decimal Qty,
    string Unit,
    decimal Net)
{
    // Fest auf de-CH und nie ueber CurrentCulture: Sonst zeigt derselbe Ausdruck je
    // nach Windows-Einstellung andere Zahlen.
    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    /// <summary>Menge mit Einheit, z. B. "24 m" oder "24.50 m".</summary>
    public string QtyText
    {
        get
        {
            var zahl = Qty == decimal.Truncate(Qty)
                ? Qty.ToString("N0", Ch)
                : Qty.ToString("N2", Ch);

            return Unit.Length == 0 ? zahl : $"{zahl} {Unit}";
        }
    }

    /// <summary>Nettobetrag ohne Waehrung, z. B. "12'500".</summary>
    public string NetText => Math.Round(Net, 0, MidpointRounding.AwayFromZero).ToString("N0", Ch);
}

/// <summary>
/// Zaehlt die Mengen der Sanierungsverfahren (Inliner GFK, Inliner Nadelfilz,
/// Kurzliner, Manschetten, Linerendmanschetten) aus einem Kostenspeicher.
///
/// Bewusst dieselben Regeln wie die Kostenzusammenstellung: Erkennung ueber
/// <see cref="SpecialStatsClassifier"/> und nur ausgewaehlte Zeilen. Sonst koennten
/// Cockpit und Angebots-PDF verschiedene Mengen fuer dasselbe Projekt zeigen.
/// </summary>
public static class RehabilitationQuantityCalculator
{
    public static IReadOnlyList<RehabilitationQuantity> Calculate(ProjectCostStore? costs)
    {
        var buckets = SpecialStatsClassifier.CreateSpecialStatsBuckets();
        if (costs is null)
            return [];

        foreach (var line in costs.ByHolding.Values
                     .SelectMany(cost => cost.Measures)
                     .SelectMany(measure => measure.Lines)
                     .Where(line => line.Selected))
        {
            if (!SpecialStatsClassifier.TryResolveSpecialStatsCategory(line, out var category))
                continue;
            if (!buckets.TryGetValue(category, out var bucket))
                continue;

            bucket.TotalQty += line.Qty;
            bucket.TotalNet += line.Qty * line.UnitPrice;

            var unit = SpecialStatsClassifier.NormalizeUnit(line.Unit);
            if (unit.Length > 0)
                bucket.Units.Add(unit);
        }

        // Feste Reihenfolge aus der Konfiguration; leere Verfahren bleiben weg.
        return SpecialStatsClassifier.SpecialStatsConfigs
            .Where(cfg => buckets[cfg.Category].TotalQty > 0m)
            .Select(cfg =>
            {
                var bucket = buckets[cfg.Category];
                return new RehabilitationQuantity(
                    cfg.Category,
                    cfg.Label,
                    bucket.TotalQty,
                    SpecialStatsClassifier.ResolveDisplayUnit(bucket),
                    bucket.TotalNet);
            })
            .ToList();
    }

    /// <summary>Zaehlt ueber mehrere Kostenspeicher hinweg (z. B. Haltungen und Schaechte).</summary>
    public static IReadOnlyList<RehabilitationQuantity> CalculateCombined(params ProjectCostStore?[] stores)
    {
        var zusammen = new ProjectCostStore();
        var lauf = 0;
        foreach (var store in stores ?? [])
        {
            if (store is null)
                continue;

            foreach (var (holding, cost) in store.ByHolding)
                zusammen.ByHolding[$"{lauf}|{holding}"] = cost;

            lauf++;
        }

        return Calculate(zusammen);
    }
}
