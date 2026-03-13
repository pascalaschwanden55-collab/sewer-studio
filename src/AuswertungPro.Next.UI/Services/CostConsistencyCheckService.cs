using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Deterministic rule-based consistency checker for cost calculations.
/// Produces warnings (KK01–KK14) without blocking save operations.
/// </summary>
public sealed class CostConsistencyCheckService
{
    public IReadOnlyList<ConsistencyWarning> CheckAll(
        IReadOnlyList<MeasureBlockVm> blocks,
        IReadOnlyDictionary<string, CostCatalogItem> catalog,
        IReadOnlyDictionary<string, MeasureTemplate> templates,
        ProjectCostStore? projectStore,
        string? currentHolding)
    {
        var warnings = new List<ConsistencyWarning>();

        foreach (var block in blocks)
            CheckBlock(block, catalog, templates, warnings);

        // KK14: Total 0 despite selected measures
        if (blocks.Count > 0 && blocks.Any(b => b.Lines.Any(l => l.Selected)))
        {
            var total = blocks.Sum(b => b.Total);
            if (total == 0m)
            {
                warnings.Add(new ConsistencyWarning
                {
                    RuleId = "KK14",
                    Severity = ConsistencyWarningSeverity.Warning,
                    Message = "Gesamtkosten sind 0.00 CHF obwohl Massnahmen ausgewaehlt sind."
                });
            }
        }

        if (projectStore is not null && !string.IsNullOrWhiteSpace(currentHolding))
            CheckCrossHaltung(projectStore, currentHolding, warnings);

        return warnings;
    }

    private static void CheckBlock(
        MeasureBlockVm block,
        IReadOnlyDictionary<string, CostCatalogItem> catalog,
        IReadOnlyDictionary<string, MeasureTemplate> templates,
        List<ConsistencyWarning> warnings)
    {
        var hasSelectedLines = false;
        var hasByDnItem = false;
        var hasMeterLine = false;
        var hasConnectionLine = false;

        foreach (var line in block.Lines)
        {
            if (line.Selected)
                hasSelectedLines = true;

            // KK01: Price 0 on selected line (not missing — user set it to 0)
            if (line.Selected && line.UnitPrice == 0m && !line.PriceMissing)
            {
                // Check if catalog has a non-zero price for this item
                var hasCatalogPrice = catalog.TryGetValue(line.ItemKey, out var catItem)
                    && ((catItem.Price.HasValue && catItem.Price.Value > 0)
                        || catItem.DnPrices.Any(d => d.Price > 0));

                if (hasCatalogPrice)
                {
                    warnings.Add(new ConsistencyWarning
                    {
                        RuleId = "KK01",
                        Severity = ConsistencyWarningSeverity.Error,
                        MeasureId = block.MeasureId,
                        ItemKey = line.ItemKey,
                        Message = $"Position '{Truncate(line.Text)}' ist aktiviert aber hat Preis 0.00 CHF.",
                        SuggestedFix = catItem!.Price.HasValue ? $"{catItem.Price.Value:N2} CHF" : null
                    });
                }
            }

            // KK02: Missing catalog price
            if (line.Selected && line.PriceMissing)
            {
                warnings.Add(new ConsistencyWarning
                {
                    RuleId = "KK02",
                    Severity = ConsistencyWarningSeverity.Error,
                    MeasureId = block.MeasureId,
                    ItemKey = line.ItemKey,
                    Message = $"Position '{Truncate(line.Text)}' hat keinen Katalogpreis (Preis fehlt)."
                });
            }

            // KK03: Missing unit
            if (line.Selected && string.IsNullOrWhiteSpace(line.Unit))
            {
                warnings.Add(new ConsistencyWarning
                {
                    RuleId = "KK03",
                    Severity = ConsistencyWarningSeverity.Warning,
                    MeasureId = block.MeasureId,
                    ItemKey = line.ItemKey,
                    Message = $"Position '{Truncate(line.Text)}' hat keine Einheit definiert."
                });
            }

            // KK04: Qty 0 on selected line
            if (line.Selected && line.Qty == 0m)
            {
                warnings.Add(new ConsistencyWarning
                {
                    RuleId = "KK04",
                    Severity = ConsistencyWarningSeverity.Warning,
                    MeasureId = block.MeasureId,
                    ItemKey = line.ItemKey,
                    Message = $"Position '{Truncate(line.Text)}' ist aktiviert aber Menge ist 0."
                });
            }

            // KK06: Price deviation >50% from catalog
            if (line.Selected && line.UnitPrice > 0 && !line.PriceMissing
                && catalog.TryGetValue(line.ItemKey, out var catalogItem))
            {
                var catalogPrice = ResolveCatalogPrice(catalogItem, block.DnText);
                if (catalogPrice.HasValue && catalogPrice.Value > 0)
                {
                    var deviation = Math.Abs(line.UnitPrice - catalogPrice.Value) / catalogPrice.Value;
                    if (deviation > 0.50m)
                    {
                        warnings.Add(new ConsistencyWarning
                        {
                            RuleId = "KK06",
                            Severity = ConsistencyWarningSeverity.Warning,
                            MeasureId = block.MeasureId,
                            ItemKey = line.ItemKey,
                            Message = $"Position '{Truncate(line.Text)}': Preis ({line.UnitPrice:N2} CHF) weicht >{deviation:P0} vom Katalogpreis ({catalogPrice.Value:N2} CHF) ab.",
                            SuggestedFix = $"{catalogPrice.Value:N2} CHF"
                        });
                    }
                }
            }

            // KK07: Price manually overridden (info only)
            if (line.Selected && line.IsPriceOverridden)
            {
                warnings.Add(new ConsistencyWarning
                {
                    RuleId = "KK07",
                    Severity = ConsistencyWarningSeverity.Info,
                    MeasureId = block.MeasureId,
                    ItemKey = line.ItemKey,
                    Message = $"Position '{Truncate(line.Text)}': Preis manuell ueberschrieben ({line.UnitPrice:N2} CHF)."
                });
            }

            // KK12: Text contains "Kanalroboter" but unit != "Std"
            if (line.Selected && !string.IsNullOrWhiteSpace(line.Text)
                && line.Text.Contains("Kanalroboter", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(line.Unit)
                && !string.Equals(line.Unit.Trim(), "Std", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(line.Unit.Trim(), "h", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(new ConsistencyWarning
                {
                    RuleId = "KK12",
                    Severity = ConsistencyWarningSeverity.Warning,
                    MeasureId = block.MeasureId,
                    ItemKey = line.ItemKey,
                    Message = $"Position '{Truncate(line.Text)}': Text enthaelt 'Kanalroboter' aber Einheit ist '{line.Unit}' statt 'Std'."
                });
            }

            // Track what the block needs
            if (catalog.TryGetValue(line.ItemKey, out var ci)
                && string.Equals(ci.Type, "ByDN", StringComparison.OrdinalIgnoreCase))
                hasByDnItem = true;

            if (IsMeterUnit(line.Unit))
                hasMeterLine = true;

            if (IsConnectionLine(line))
                hasConnectionLine = true;
        }

        // KK08: DN missing but ByDN items exist
        if (hasByDnItem && string.IsNullOrWhiteSpace(block.DnText))
        {
            warnings.Add(new ConsistencyWarning
            {
                RuleId = "KK08",
                Severity = ConsistencyWarningSeverity.Error,
                MeasureId = block.MeasureId,
                Message = $"Massnahme '{block.MeasureName}': DN fehlt, aber Positionen benoetigen DN fuer Preisermittlung."
            });
        }

        // KK09: Length missing but meter-unit lines exist
        if (hasMeterLine && string.IsNullOrWhiteSpace(block.LengthText))
        {
            warnings.Add(new ConsistencyWarning
            {
                RuleId = "KK09",
                Severity = ConsistencyWarningSeverity.Error,
                MeasureId = block.MeasureId,
                Message = $"Massnahme '{block.MeasureName}': Laenge fehlt, aber Positionen haben Einheit 'm'."
            });
        }

        // KK11: No selected lines
        if (!hasSelectedLines && block.Lines.Count > 0)
        {
            warnings.Add(new ConsistencyWarning
            {
                RuleId = "KK11",
                Severity = ConsistencyWarningSeverity.Warning,
                MeasureId = block.MeasureId,
                Message = $"Massnahme '{block.MeasureName}' hat keine aktivierten Positionen."
            });
        }

        // KK13: Connection lines present but 0 connections
        if (hasConnectionLine && string.IsNullOrWhiteSpace(block.ConnectionsText))
        {
            var anyConnectionSelected = block.Lines.Any(l => l.Selected && IsConnectionLine(l));
            if (!anyConnectionSelected)
            {
                warnings.Add(new ConsistencyWarning
                {
                    RuleId = "KK13",
                    Severity = ConsistencyWarningSeverity.Info,
                    MeasureId = block.MeasureId,
                    Message = "Anschluss-Positionen sind deaktiviert (0 Anschluesse erkannt)."
                });
            }
        }

        // KK05: Template deviation — catalog price vs current price
        if (templates.TryGetValue(block.MeasureId, out var template))
        {
            foreach (var tmplLine in template.Lines)
            {
                if (!tmplLine.Enabled) continue;

                var matchingLine = block.Lines.FirstOrDefault(l =>
                    string.Equals(l.ItemKey, tmplLine.ItemKey, StringComparison.OrdinalIgnoreCase)
                    && l.Selected);
                if (matchingLine is null) continue;

                if (catalog.TryGetValue(tmplLine.ItemKey, out var tmplCatItem))
                {
                    var expectedPrice = ResolveCatalogPrice(tmplCatItem, block.DnText);
                    if (expectedPrice.HasValue && expectedPrice.Value > 0 && matchingLine.UnitPrice == 0m)
                    {
                        warnings.Add(new ConsistencyWarning
                        {
                            RuleId = "KK05",
                            Severity = ConsistencyWarningSeverity.Warning,
                            MeasureId = block.MeasureId,
                            ItemKey = matchingLine.ItemKey,
                            Message = $"Position '{Truncate(matchingLine.Text)}': Vorlage definiert Preis {expectedPrice.Value:N2} CHF, aktuell 0.00 CHF.",
                            SuggestedFix = $"{expectedPrice.Value:N2} CHF"
                        });
                    }
                }
            }
        }
    }

    // KK10: Cross-Haltung price deviation
    private static void CheckCrossHaltung(
        ProjectCostStore store,
        string currentHolding,
        List<ConsistencyWarning> warnings)
    {
        var pricesByKey = new Dictionary<string, List<(string Holding, decimal Price, string Text)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (holding, cost) in store.ByHolding)
        {
            foreach (var measure in cost.Measures)
            foreach (var line in measure.Lines)
            {
                if (!line.Selected || line.UnitPrice <= 0) continue;

                if (!pricesByKey.TryGetValue(line.ItemKey, out var list))
                {
                    list = new();
                    pricesByKey[line.ItemKey] = list;
                }
                list.Add((holding, line.UnitPrice, line.Text));
            }
        }

        foreach (var (key, entries) in pricesByKey)
        {
            if (entries.Count < 2) continue;

            var min = entries.Min(e => e.Price);
            var max = entries.Max(e => e.Price);
            if (min <= 0) continue;

            var deviation = (max - min) / min;
            if (deviation > 0.10m)
            {
                var minEntry = entries.First(e => e.Price == min);
                var maxEntry = entries.First(e => e.Price == max);
                warnings.Add(new ConsistencyWarning
                {
                    RuleId = "KK10",
                    Severity = ConsistencyWarningSeverity.Warning,
                    ItemKey = key,
                    Message = $"Position '{Truncate(minEntry.Text)}': Preis {minEntry.Price:N2} in {minEntry.Holding} vs. {maxEntry.Price:N2} in {maxEntry.Holding} (>{deviation:P0} Abweichung)."
                });
            }
        }
    }

    private static decimal? ResolveCatalogPrice(CostCatalogItem item, string? dnText)
    {
        if (string.Equals(item.Type, "Fixed", StringComparison.OrdinalIgnoreCase))
            return item.Price;

        if (string.Equals(item.Type, "ByDN", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(dnText)
            && int.TryParse(dnText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dn))
        {
            var match = item.DnPrices.FirstOrDefault(d => dn >= d.DnFrom && dn <= d.DnTo);
            return match?.Price;
        }

        return null;
    }

    private static bool IsMeterUnit(string? unit)
        => string.Equals(unit?.Trim(), "m", StringComparison.OrdinalIgnoreCase);

    private static bool IsConnectionLine(CostLineVm line)
    {
        if (!string.IsNullOrWhiteSpace(line.ItemKey)
            && line.ItemKey.Contains("ANSCHLUSS", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(line.Text)
            && line.Text.Contains("anschluss", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string Truncate(string? text, int max = 40)
    {
        if (string.IsNullOrWhiteSpace(text)) return "?";
        return text.Length <= max ? text : text[..max] + "...";
    }
}
