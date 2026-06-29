using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Erzwingt Installationsregeln und die Linerende-Manschetten-Regel auf Domain-Ebene.
/// Gibt zurueck, ob Zeilen veraendert wurden (damit der Aufrufer UpdateTotal aufrufen kann).
/// </summary>
public static class MeasureRuleService
{
    // Konstanten fuer Installation
    private const string InstallUvAnlageKey = "INSTALL_UV_ANLAGE";
    private const string InstallHlAnlageKey = "INSTALL_HL_ANLAGE";

    // Konstanten fuer Linerende-Manschette
    private const string LinerEndManschetteKey = "LINERENDMANSCHETTE_LEM";
    private const int EndManschetteMinDn = 200;
    private const decimal EndManschetteDefaultQty = 2m;

    /// <summary>
    /// Gibt den Schluessel des erforderlichen Installations-Katalogeintrags zurueck.
    /// Null = keine Installationsregel fuer diese Massnahme.
    /// </summary>
    public static string? GetRequiredInstallationItemKey(string measureId, string measureName)
    {
        var descriptor = $"{measureId} {measureName}";
        if (descriptor.Contains("GFK", StringComparison.OrdinalIgnoreCase))
            return InstallUvAnlageKey;
        if (descriptor.Contains("NADELFILZ", StringComparison.OrdinalIgnoreCase))
            return InstallHlAnlageKey;
        return null;
    }

    /// <summary>
    /// Stellt sicher, dass genau die erforderliche Installations-Zeile vorhanden und aktiv ist.
    /// Gibt eine Liste neuer Zeilen zurueck, die hinzugefuegt werden muessen, sowie
    /// die Schluesselliste von Zeilen, die entfernt werden muessen.
    /// </summary>
    /// <param name="lines">Bestehende Zeilen des Massnahmen-Blocks.</param>
    /// <param name="catalog">Aktiver Preiskatalog (Pruefung ob Schuessel bekannt ist).</param>
    /// <param name="requiredInstallKey">Wert von <see cref="GetRequiredInstallationItemKey"/>.</param>
    /// <param name="linesToRemove">Installations-Zeilen, die aus dem Block entfernt werden sollen.</param>
    /// <param name="lineToAdd">Neue Installations-Zeile, die hinzugefuegt werden soll (null = keine).</param>
    /// <param name="changed">true, wenn mindestens eine Aenderung vorgenommen wurde.</param>
    public static void EnforceInstallationRule(
        IList<CostLine> lines,
        IReadOnlyDictionary<string, CostCatalogItem> catalog,
        string requiredInstallKey,
        out IReadOnlyList<CostLine> linesToRemove,
        out CostLine? lineToAdd,
        out bool changed)
    {
        linesToRemove = Array.Empty<CostLine>();
        lineToAdd = null;
        changed = false;

        if (!catalog.TryGetValue(requiredInstallKey, out var catalogItem))
            return;

        var installationLines = lines
            .Where(l => CostCalculatorLogicService.IsInstallationLine(l.Group, l.ItemKey))
            .ToList();

        // Fehlende Pflicht-Zeile hinzufuegen
        if (!installationLines.Any(l => CostCalculatorLogicService.IsItemKey(l.ItemKey, requiredInstallKey)))
        {
            lineToAdd = new CostLine
            {
                Group = CatalogItemGrouping.DeriveGroupFromKey(catalogItem.Key),
                ItemKey = catalogItem.Key,
                Text = catalogItem.Name,
                Unit = catalogItem.Unit,
                Qty = 1m,
                Selected = true,
                UnitPrice = string.Equals(catalogItem.Type, "Fixed", StringComparison.OrdinalIgnoreCase) && catalogItem.Price.HasValue
                    ? catalogItem.Price.Value
                    : 0m
            };
            changed = true;

            // Nach virtuellem Hinzufuegen neu bauen fuer den Rest der Pruefung
            installationLines = installationLines
                .Append(lineToAdd)
                .ToList();
        }

        // Falsche Installations-Zeilen entfernen; richtige reaktivieren
        var toRemove = new List<CostLine>();
        foreach (var line in installationLines)
        {
            if (CostCalculatorLogicService.IsItemKey(line.ItemKey, requiredInstallKey))
            {
                // Sicherstellen, dass die Pflicht-Zeile aktiv und Menge > 0 ist
                if (!line.Selected)
                {
                    line.Selected = true;
                    changed = true;
                }
                if (line.Qty <= 0m)
                {
                    line.Qty = 1m;
                    changed = true;
                }
                continue;
            }

            // Andere Installations-Zeilen entfernen
            toRemove.Add(line);
            changed = true;
        }
        linesToRemove = toRemove;
    }

    /// <summary>
    /// Wendet die Linerende-Manschetten-Regel an (nur ab DN 200; Standard 2 Stk).
    /// </summary>
    /// <param name="lines">Bestehende Zeilen des Massnahmen-Blocks (wird in-place veraendert).</param>
    /// <param name="dn">Aktueller Nennweiten-Wert (null = Regel nicht anwenden).</param>
    /// <param name="changed">true, wenn mindestens eine Zeile veraendert wurde.</param>
    public static void EnforceEndManschetteRule(
        IList<CostLine> lines,
        int? dn,
        out bool changed)
    {
        changed = false;

        var lemLines = lines
            .Where(l => CostCalculatorLogicService.IsItemKey(l.ItemKey, LinerEndManschetteKey))
            .ToList();

        if (lemLines.Count == 0 || dn is null)
            return;

        var allowed = dn.Value >= EndManschetteMinDn;

        foreach (var line in lemLines)
        {
            if (!allowed)
            {
                // Unter DN 200: Endmanschette deaktivieren
                if (line.Selected || line.Qty != 0m)
                {
                    line.Qty = 0m;
                    line.IsQtyOverridden = false;
                    line.Selected = false;
                    changed = true;
                }
                continue;
            }

            // Ab DN 200: frisch deaktivierte Zeile reaktivieren
            if (!line.Selected && line.Qty == 0m)
            {
                line.Selected = true;
                changed = true;
            }
            if (!line.IsQtyOverridden && line.Qty <= 0m)
            {
                line.Qty = EndManschetteDefaultQty;
                changed = true;
            }
        }
    }
}
