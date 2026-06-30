using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Baut headless (ohne WPF-ViewModel) fuer eine Haltung + gewaehlte Hauptarbeit das
/// komplette Massnahmen-Buendel mit Auto-Mengen (DN, Laenge, Anschluss-Dedup) und
/// denselben Regeln (Endmanschette ab DN200, Anschluss-Zeilen aus Dedup-Zahl) wie
/// das Einzelfenster.
///
/// Entspricht dem alten <c>HoldingMeasureFactory</c> aus <c>UI.ViewModels.Windows</c>,
/// arbeitet aber ausschliesslich auf Domain-Typen (CostLine / MeasureCost / HoldingCost)
/// ueber <see cref="MeasurePricingEngine"/>, <see cref="MeasureRuleService"/> und
/// <see cref="MeasureImportDefaultsResolver"/>. Kein WPF, keine ObservableCollection.
///
/// <paramref name="extraOptionKeys"/> aktiviert ankreuzbare Zusatzpositionen
/// (Verkehrsdienst, Wasserhaltung, Fraesen, Dichtheitspruefung, Dokumentation), die als
/// deaktivierte Zeilen im Buendel liegen. <paramref name="hauptarbeitMenge"/> uebersteuert
/// die Menge der Hauptarbeit-Zeile manuell (z.B. Stueckzahl bei Reparatur).
/// </summary>
public static class HoldingMeasureFactory
{
    public static HoldingCost? Build(
        string holdingName,
        HaltungRecord? record,
        string measureId,
        IReadOnlyDictionary<string, MeasureTemplate> templates,
        IReadOnlyDictionary<string, CostCatalogItem> catalog,
        decimal vatRate,
        IReadOnlyCollection<string>? extraOptionKeys = null,
        decimal? hauptarbeitMenge = null,
        string? hauptarbeitItemKey = null)
    {
        if (string.IsNullOrWhiteSpace(measureId) || templates is null || catalog is null)
            return null;
        if (!templates.TryGetValue(measureId, out var template) || template is null || template.Disabled)
            return null;

        // --- Schritt 1: Zeilen aus Template bauen (gleiche Sortierung wie MeasureBlockVm) ---
        var lines = BuildLinesFromTemplate(template, catalog);

        // --- Schritt 2: Import-Defaults aus HaltungRecord lesen ---
        var defaults = record is not null
            ? MeasureImportDefaultsResolver.Resolve(record)
            : new MeasureImportDefaultsResolver.ImportDefaults(null, null, 0);

        // --- Schritt 3: DN anwenden -> Katalogpreise + Endmanschetten-Regel ---
        if (defaults.Dn.HasValue)
        {
            MeasurePricingEngine.ApplyCatalogPrices(lines, catalog, defaults.Dn, onlyQtyBased: false);
            MeasureRuleService.EnforceEndManschetteRule(lines, defaults.Dn, out _);
        }

        // --- Schritt 3b: Pflicht-Installationszeile erzwingen (analog zum VM-Pfad) ---
        // Der alte MeasureBlockVm-Konstruktor + OnLineChanged erzwang die richtige
        // Installationszeile (INSTALL_UV_ANLAGE bei GFK, INSTALL_HL_ANLAGE bei NADELFILZ).
        var requiredInstallKey = MeasureRuleService.GetRequiredInstallationItemKey(measureId, template.Name);
        if (requiredInstallKey is not null)
        {
            MeasureRuleService.EnforceInstallationRule(
                lines, catalog, requiredInstallKey,
                out var linesToRemove, out var lineToAdd, out _);
            foreach (var l in linesToRemove)
                lines.Remove(l);
            if (lineToAdd is not null)
                lines.Add(lineToAdd);
        }

        // --- Schritt 4: Laenge auf alle m-Zeilen anwenden ---
        var roundedLength = defaults.LengthMeters.HasValue
            ? Math.Round(defaults.LengthMeters.Value, 2)
            : (decimal?)null;
        if (roundedLength.HasValue)
            MeasurePricingEngine.ApplyLengthToLines(lines, roundedLength.Value);

        // --- Schritt 5: Anschlussanzahl anwenden ---
        MeasurePricingEngine.ApplyConnectionsToLines(lines, defaults.Connections);

        // --- Schritt 6: Zusatzoptionen aktivieren ---
        if (extraOptionKeys is { Count: > 0 })
        {
            foreach (var key in extraOptionKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                foreach (var line in lines.Where(l =>
                             string.Equals(l.ItemKey, key, StringComparison.OrdinalIgnoreCase)))
                {
                    line.Selected = true;
                }
            }
        }

        // --- Schritt 7: Hauptarbeit-Menge manuell uebersteuern ---
        // Selected=true erzwingen, falls die Anschluss-Auto-Logik die gewaehlte Hauptarbeit
        // bei 0 erkannten Anschluessen deaktiviert haette (z.B. ANSCHLUSS_EINBINDEN).
        if (hauptarbeitMenge.HasValue && hauptarbeitMenge.Value > 0m)
        {
            var hauptKey = string.IsNullOrWhiteSpace(hauptarbeitItemKey) ? measureId : hauptarbeitItemKey;
            foreach (var line in lines.Where(l =>
                         string.Equals(l.ItemKey, hauptKey, StringComparison.OrdinalIgnoreCase)))
            {
                line.Selected = true;
                line.Qty = hauptarbeitMenge.Value;
                line.IsQtyOverridden = true;
            }
        }

        // --- Schritt 8: Abschliessender vollstaendiger Preisdurchlauf ---
        MeasurePricingEngine.ApplyCatalogPrices(lines, catalog, defaults.Dn, onlyQtyBased: false);

        // --- Schritt 9: MeasureCost + HoldingCost aufbauen ---
        var total = lines.Where(l => l.Selected).Sum(l => l.Qty * l.UnitPrice);
        var measure = new MeasureCost
        {
            MeasureId = measureId,
            MeasureName = template.Name,
            Dn = defaults.Dn,
            LengthMeters = roundedLength,
            Lines = lines,
            Total = total
        };

        return CostCalculatorLogicService.BuildHoldingCost(
            holdingName ?? "", null, new[] { measure }, vatRate);
    }

    /// <summary>
    /// Baut eine geordnete Liste von <see cref="CostLine"/> aus dem Template,
    /// analog zur Zeilen-Erzeugung in <c>MeasureBlockVm</c>.
    /// Zeilen werden zuerst nach Gruppen-Reihenfolge, dann nach Template-Index sortiert.
    /// </summary>
    private static List<CostLine> BuildLinesFromTemplate(
        MeasureTemplate template,
        IReadOnlyDictionary<string, CostCatalogItem> catalog)
    {
        return template.Lines
            .Select((tl, index) => new { TemplateLine = tl, Index = index })
            .OrderBy(x => CatalogItemGrouping.GetGroupOrder(x.TemplateLine.Group))
            .ThenBy(x => x.Index)
            .Select(x =>
            {
                var tl = x.TemplateLine;
                catalog.TryGetValue(tl.ItemKey ?? "", out var item);

                return new CostLine
                {
                    Group = tl.Group ?? "",
                    ItemKey = tl.ItemKey ?? "",
                    Text = item?.Name ?? tl.ItemKey ?? "",
                    Unit = item?.Unit ?? "",
                    Qty = tl.DefaultQty,
                    Selected = tl.Enabled,
                    IsPriceOverridden = false,
                    IsQtyOverridden = false
                };
            })
            .ToList();
    }
}
