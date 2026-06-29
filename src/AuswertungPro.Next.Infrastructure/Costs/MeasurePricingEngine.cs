using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Wendet Katalogpreise und Mengenregeln auf eine Liste von <see cref="CostLine"/>-Objekten an.
/// Arbeitet ausschliesslich auf Domain-Typen (keine WPF-Abhaengigkeiten).
/// </summary>
public static class MeasurePricingEngine
{
    /// <summary>
    /// Wendet Katalogpreise auf alle Zeilen an.
    /// Zeilen mit <see cref="CostLine.IsPriceOverridden"/> werden uebersprungen.
    /// </summary>
    /// <param name="lines">Liste der Kostenpositionen (wird in-place veraendert).</param>
    /// <param name="catalog">Aktiver Preiskatalog.</param>
    /// <param name="dn">Aktueller Nennweiten-Wert (null = unbekannt).</param>
    /// <param name="onlyQtyBased">
    ///   true  = nur Preise aktualisieren, wenn der Katalogeintrag Mengenregeln (QtyFrom/QtyTo) hat.
    ///   false = alle Preise aktualisieren.
    /// </param>
    public static void ApplyCatalogPrices(
        IList<CostLine> lines,
        IReadOnlyDictionary<string, CostCatalogItem> catalog,
        int? dn,
        bool onlyQtyBased)
    {
        foreach (var line in lines)
        {
            if (line.IsPriceOverridden)
                continue;

            if (!catalog.TryGetValue(line.ItemKey, out var item))
            {
                if (!onlyQtyBased)
                {
                    line.UnitPrice = 0m;
                    line.PriceHint = "";
                }
                continue;
            }

            var hasQtyRules = item.DnPrices.Any(p => p.QtyFrom.HasValue || p.QtyTo.HasValue);
            if (onlyQtyBased && !hasQtyRules)
                continue;

            // Festpreis-Typ
            if (string.Equals(item.Type, "Fixed", StringComparison.OrdinalIgnoreCase))
            {
                if (!onlyQtyBased)
                {
                    line.UnitPrice = item.Price ?? 0m;
                    line.PriceHint = item.Price.HasValue ? "" : "";
                }
                continue;
            }

            // ByDN-Typ
            if (!string.Equals(item.Type, "ByDN", StringComparison.OrdinalIgnoreCase))
                continue;

            if (dn is null)
            {
                if (!onlyQtyBased)
                {
                    line.UnitPrice = 0m;
                    line.PriceHint = "";
                }
                continue;
            }

            var candidates = item.DnPrices
                .Where(x => dn >= x.DnFrom && dn <= x.DnTo)
                .ToList();
            var usedNearestFallback = false;

            if (candidates.Count == 0)
            {
                candidates = CostCalculatorLogicService.FindNearestDnCandidates(item.DnPrices, dn.Value);
                usedNearestFallback = candidates.Count > 0;
                if (candidates.Count == 0)
                {
                    if (!onlyQtyBased)
                    {
                        line.UnitPrice = 0m;
                        line.PriceHint = "";
                    }
                    continue;
                }
            }

            DnPrice? match;
            if (hasQtyRules)
            {
                match = candidates.FirstOrDefault(x => CostCalculatorLogicService.QtyMatches(x, line.Qty));
                match ??= candidates.FirstOrDefault(x => !x.QtyFrom.HasValue && !x.QtyTo.HasValue);
                match ??= candidates[0];
            }
            else
            {
                match = candidates[0];
            }

            line.UnitPrice = match?.Price ?? 0m;
            line.PriceHint = usedNearestFallback && match is not null
                ? CostCalculatorLogicService.BuildNearestDnPriceHint(match)
                : "";
        }
    }

    /// <summary>
    /// Setzt die Menge aller Meter-Positionen (Einheit "m") auf <paramref name="lengthM"/>,
    /// sofern die Menge nicht manuell ueberschrieben wurde (<see cref="CostLine.IsQtyOverridden"/>).
    /// </summary>
    public static void ApplyLengthToLines(IList<CostLine> lines, decimal lengthM)
    {
        foreach (var line in lines)
        {
            if (!CostCalculatorLogicService.IsMeterUnit(line.Unit))
                continue;
            if (line.IsQtyOverridden)
                continue;

            line.Qty = lengthM;
        }
    }

    /// <summary>
    /// Setzt die Menge aller Anschluss-Positionen auf <paramref name="connections"/>.
    /// Bei 0 oder negativ werden die Zeilen deaktiviert.
    /// </summary>
    public static void ApplyConnectionsToLines(IList<CostLine> lines, decimal connections)
    {
        var disable = connections <= 0m;

        foreach (var line in lines)
        {
            if (!CostCalculatorLogicService.IsConnectionLine(line.ItemKey, line.Text))
                continue;

            if (disable)
            {
                line.Qty = 0m;
                line.IsQtyOverridden = false;
                line.Selected = false;
                line.TransferMarked = false;
                continue;
            }

            // Zeile reaktivieren, wenn sie durch "0 Anschluesse" abgeschaltet wurde.
            if (!line.Selected && line.Qty == 0m)
                line.Selected = true;

            if (line.IsQtyOverridden)
                continue;

            line.Qty = connections;
        }
    }

    /// <summary>
    /// Liest die Anschluss-Menge aus vorhandenen Zeilen, falls ConnectionsText noch leer ist.
    /// Gibt null zurueck, wenn kein positiver Wert gefunden wurde.
    /// </summary>
    public static decimal? TryReadConnectionsFromLines(IEnumerable<CostLine> lines)
    {
        var qty = lines
            .Where(l => CostCalculatorLogicService.IsConnectionLine(l.ItemKey, l.Text))
            .Where(l => l.Selected && l.Qty > 0)
            .Select(l => l.Qty)
            .DefaultIfEmpty(0m)
            .Max();

        return qty > 0 ? qty : null;
    }
}
