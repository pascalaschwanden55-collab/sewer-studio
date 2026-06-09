using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.Infrastructure.Vsa;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>
/// Baut headless (ohne Kostenfenster) für eine Haltung + gewählte Hauptarbeit das
/// komplette Massnahmen-Bündel mit Auto-Mengen (DN, Länge, Anschluss-Dedup) und
/// denselben Regeln (Endmanschette ab DN200, Anschluss-Zeilen aus Dedup-Zahl) wie
/// das Einzelfenster — durch Wiederverwendung von <see cref="MeasureBlockVm"/>.
/// So liefert die Massen-Matrix exakt dasselbe Ergebnis wie das Kostenfenster.
///
/// Die Mengen-Logik spiegelt CostCalculatorViewModel.InitializeFromHaltungRecord.
/// <paramref name="extraOptionKeys"/> aktiviert ankreuzbare Zusatzpositionen
/// (Verkehrsdienst, Wasserhaltung, Fräsen, Dichtheitsprüfung, Dokumentation), die als
/// deaktivierte Zeilen im Bündel liegen. <paramref name="hauptarbeitMenge"/> übersteuert
/// die Menge der Hauptarbeit-Zeile manuell (z.B. Stückzahl bei Reparatur).
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
        decimal? hauptarbeitMenge = null)
    {
        if (string.IsNullOrWhiteSpace(measureId) || templates is null || catalog is null)
            return null;
        if (!templates.TryGetValue(measureId, out var template) || template is null || template.Disabled)
            return null;

        var block = new MeasureBlockVm(template, catalog);

        // DN
        var dnValue = record?.GetFieldValue("DN_mm");
        if (!string.IsNullOrWhiteSpace(dnValue) && int.TryParse(dnValue, out var dn))
            block.SetDnFromImport(dn.ToString());

        // Länge (m) — füllt alle m-Zeilen (auch noch deaktivierte Optionen wie Fräsen)
        var lengthValue = record?.GetFieldValue("Haltungslaenge_m");
        if (!string.IsNullOrWhiteSpace(lengthValue) && decimal.TryParse(lengthValue, out var length))
            block.SetLengthFromImport(length.ToString("0.00"));

        // Anschlüsse aus Schadenscodierung (Dedup); kein Wert -> 0 (deaktiviert Anschluss-Zeilen).
        var connections = (record is null ? null : ConnectionCountEstimator.EstimateFromRecord(record)) ?? 0;
        block.SetConnectionsFromImport(connections.ToString(CultureInfo.InvariantCulture));

        // Zusatzoptionen aktivieren (Zeilen liegen als enabled=false im Bündel).
        if (extraOptionKeys is { Count: > 0 })
        {
            foreach (var key in extraOptionKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                foreach (var line in block.Lines.Where(l =>
                             string.Equals(l.ItemKey, key, System.StringComparison.OrdinalIgnoreCase)))
                {
                    line.Selected = true;
                }
            }
        }

        // Hauptarbeit-Menge manuell übersteuern (Stückzahl bei Reparatur). Direkte
        // Qty-Zuweisung setzt IsQtyOverridden, sodass die Auto-Mengen sie nicht zurücksetzen.
        if (hauptarbeitMenge.HasValue && hauptarbeitMenge.Value > 0m)
        {
            foreach (var line in block.Lines.Where(l =>
                         string.Equals(l.ItemKey, measureId, System.StringComparison.OrdinalIgnoreCase)))
            {
                line.Qty = hauptarbeitMenge.Value;
            }
        }

        block.ApplyCatalogPrices();

        var measure = block.ToModel();
        return CostCalculatorLogicService.BuildHoldingCost(holdingName ?? "", null, new[] { measure }, vatRate);
    }
}
