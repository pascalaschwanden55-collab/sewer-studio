using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Cost;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Wendet aktualisierte Katalogpreise auf gespeicherte Kostenpositionen an —
/// ohne Template-Rebuild (Audit K2).
///
/// WICHTIG: ResolveExactCatalogPrice benutzt KEINEN Naechster-DN-Fallback
/// (anders als MeasurePricingEngine). Lieber Preis stehen lassen als stil
/// falsch ersetzen. Verhalten exakt wie in SanierungsMatrixPageViewModel v31d8ebc6.
/// </summary>
public static class CatalogPriceApplier
{
    /// <summary>
    /// Aktualisiert Katalogpreise auf allen gespeicherten HoldingCost-Eintraegen.
    /// Zeilen mit <see cref="CostLine.IsPriceOverridden"/> werden uebersprungen.
    /// Gibt die geaenderten Haltungsnamen zurueck (Aufrufer kann _touchedHoldings befuellen).
    /// </summary>
    public static IReadOnlyList<string> ApplyCatalogPricesToStoredCosts(
        ProjectCostStore store,
        IReadOnlyDictionary<string, CostCatalogItem> catalog,
        decimal vatRate)
    {
        var changedHoldings = new List<string>();
        foreach (var (holding, cost) in store.ByHolding)
        {
            var changed = false;
            foreach (var measure in cost.Measures)
            {
                var measureChanged = false;
                foreach (var line in measure.Lines)
                {
                    if (line.IsPriceOverridden || string.IsNullOrWhiteSpace(line.ItemKey))
                        continue;
                    if (!catalog.TryGetValue(line.ItemKey.Trim(), out var item) || !item.Active)
                        continue;

                    var price = CatalogPriceResolver.Resolve(
                        item,
                        measure.Dn,
                        line.Qty,
                        CatalogPriceResolveMode.Exact);
                    if (price.HasPrice)
                    {
                        if (price.UnitPrice != line.UnitPrice)
                        {
                            line.UnitPrice = price.UnitPrice;
                            measureChanged = true;
                        }

                        if (!string.IsNullOrWhiteSpace(line.PriceHint))
                        {
                            line.PriceHint = "";
                            measureChanged = true;
                        }
                    }
                }

                if (measureChanged)
                {
                    measure.Total = measure.Lines.Where(l => l.Selected).Sum(l => l.Qty * l.UnitPrice);
                    changed = true;
                }
            }

            if (changed)
            {
                var totals = CostCalculatorLogicService.CalculateTotals(cost.Measures.Sum(m => m.Total), vatRate);
                cost.Total = totals.Total;
                cost.MwstRate = vatRate;
                cost.MwstAmount = totals.MwstAmount;
                cost.TotalInclMwst = totals.TotalInclMwst;
                changedHoldings.Add(holding);
            }
        }

        return changedHoldings;
    }

    /// <summary>
    /// Exakter Katalogpreis: Fixed-Positionen direkt, ByDN nur bei passendem DN-/Mengen-Bereich.
    /// Bewusst KEIN Naechster-DN-Fallback — lieber Preis stehen lassen als stil falsch ersetzen.
    /// </summary>
    public static decimal? ResolveExactCatalogPrice(CostCatalogItem item, int? dn, decimal qty)
        => CatalogPriceResolver.Resolve(item, dn, qty, CatalogPriceResolveMode.Exact) is { HasPrice: true } result
            ? result.UnitPrice
            : null;
}
