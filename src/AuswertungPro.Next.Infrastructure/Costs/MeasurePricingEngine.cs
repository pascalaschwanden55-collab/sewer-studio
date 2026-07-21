using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.Cost;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Ergebnis der Preis-Berechnung fuer eine einzelne Zeile.
/// Ermoeglicht der VM, den genauen Preis-Status (HasPrice) in SetSuggestedPrice einzutragen.
/// </summary>
public readonly record struct LinePriceResult(
    string ItemKey,
    decimal UnitPrice,
    bool HasPrice,
    string PriceHint);

/// <summary>
/// Wendet Katalogpreise und Mengenregeln auf eine Liste von <see cref="CostLine"/>-Objekten an.
/// Arbeitet ausschliesslich auf Domain-Typen (keine WPF-Abhaengigkeiten).
/// </summary>
public static class MeasurePricingEngine
{
    /// <summary>
    /// Berechnet Katalogpreise fuer alle Zeilen und gibt ein Ergebnis pro Zeile zurueck.
    /// Zeilen mit <see cref="CostLine.IsPriceOverridden"/> oder ohne Aenderung erhalten null.
    /// </summary>
    public static IReadOnlyList<LinePriceResult?> ComputePrices(
        IList<CostLine> lines,
        IReadOnlyDictionary<string, CostCatalogItem> catalog,
        int? dn,
        bool onlyQtyBased)
    {
        var results = new LinePriceResult?[lines.Count];

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.IsPriceOverridden)
                continue;

            if (!catalog.TryGetValue(line.ItemKey, out var item))
            {
                if (!onlyQtyBased)
                    results[i] = new LinePriceResult(line.ItemKey, 0m, HasPrice: false, "");
                continue;
            }

            var hasQtyRules = item.DnPrices.Any(p => p.QtyFrom.HasValue || p.QtyTo.HasValue);
            if (onlyQtyBased && !hasQtyRules)
                continue;

            // Festpreis-Typ
            if (string.Equals(item.Type, "Fixed", StringComparison.OrdinalIgnoreCase))
            {
                if (!onlyQtyBased)
                    results[i] = new LinePriceResult(
                        line.ItemKey,
                        item.Price ?? 0m,
                        HasPrice: item.Price.HasValue,
                        "");
                continue;
            }

            // ByDN-Typ
            if (!string.Equals(item.Type, "ByDN", StringComparison.OrdinalIgnoreCase))
                continue;

            if (dn is null)
            {
                if (!onlyQtyBased)
                    results[i] = new LinePriceResult(line.ItemKey, 0m, HasPrice: false, "");
                continue;
            }

            var resolved = CatalogPriceResolver.Resolve(
                item,
                dn,
                line.Qty,
                CatalogPriceResolveMode.WithNearestDnFallback);

            results[i] = new LinePriceResult(
                line.ItemKey,
                resolved.UnitPrice,
                HasPrice: resolved.HasPrice,
                PriceHint: resolved.PriceHint);
        }

        return results;
    }

    /// <summary>
    /// Wendet Katalogpreise auf alle Zeilen in-place an.
    /// Zeilen mit <see cref="CostLine.IsPriceOverridden"/> werden uebersprungen.
    /// Fuer die VM: ComputePrices verwenden, damit HasPrice erhalten bleibt.
    /// </summary>
    public static void ApplyCatalogPrices(
        IList<CostLine> lines,
        IReadOnlyDictionary<string, CostCatalogItem> catalog,
        int? dn,
        bool onlyQtyBased)
    {
        var results = ComputePrices(lines, catalog, dn, onlyQtyBased);
        for (var i = 0; i < lines.Count; i++)
        {
            if (results[i] is { } r)
            {
                lines[i].UnitPrice = r.UnitPrice;
                lines[i].PriceHint = r.PriceHint;
            }
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
    /// Positive manuelle Mengen bleiben erhalten; bei 0 oder negativ werden die Zeilen deaktiviert.
    /// </summary>
    public static void ApplyConnectionsToLines(IList<CostLine> lines, decimal connections)
    {
        foreach (var line in lines)
        {
            var update = ConnectionQuantityPolicy.Evaluate(
                line.ItemKey,
                line.Text,
                line.Qty,
                line.Selected,
                connections);
            if (!update.IsApplicable)
                continue;

            if (update.ShouldDisable)
            {
                line.Qty = ConnectionQuantityPolicy.ResolveSuggestedQuantity(
                    update,
                    line.IsQtyOverridden) ?? 0m;
                line.IsQtyOverridden = false;
                line.Selected = false;
                line.TransferMarked = false;
                continue;
            }

            if (update.ShouldReactivate)
                line.Selected = true;

            if (ConnectionQuantityPolicy.ResolveSuggestedQuantity(
                    update,
                    line.IsQtyOverridden) is { } quantity)
                line.Qty = quantity;
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
