using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Waehlbare Hauptarbeit (Id=null = keine). Kategorie = Renovierung/Reparatur.
/// ManuelleMenge = Menge wird vom Anwender eingegeben (Stk oder Stunden), sonst = Haltungslaenge.
/// HauptItemKey = Katalog-Key der Hauptarbeit-Zeile (weicht bei Kanalroboter von Id ab).
/// </summary>
public sealed record MeasureOption(string? Id, string Name, string Kategorie, bool ManuelleMenge, string HauptItemKey)
{
    public override string ToString() => Name;
}

/// <summary>
/// Baut die waehlbaren Massnahmen-Optionen fuer die Sanierungs-Matrix aus Templates + Katalog.
/// Ohne WPF-Abhaengigkeiten.
/// </summary>
public static class MatrixMeasureOptionBuilder
{
    /// <summary>
    /// Baut eine geordnete Liste von Massnahmen-Optionen. Die "keine"-Option (Id=null)
    /// steht immer an erster Stelle; Renovierung vor Reparatur, danach alphabetisch.
    /// </summary>
    /// <param name="matrixMeasures">Menge der Matrix-Hauptarbeiten mit Kategorie.</param>
    /// <param name="templates">Template-Dictionary (Id -> MeasureTemplate).</param>
    /// <param name="catalog">Katalog-Dictionary (Key -> CostCatalogItem).</param>
    public static IReadOnlyList<MeasureOption> Build(
        IReadOnlyList<(string Id, string Kategorie)> matrixMeasures,
        IReadOnlyDictionary<string, MeasureTemplate> templates,
        IReadOnlyDictionary<string, CostCatalogItem> catalog)
    {
        var result = new List<MeasureOption>
        {
            // "keine" immer an erster Stelle (Id=null).
            new MeasureOption(null, "— keine —", "", false, "")
        };

        var options = new List<MeasureOption>();
        foreach (var (id, kategorie) in matrixMeasures)
        {
            if (!templates.TryGetValue(id, out var tpl))
                continue;

            // Hauptarbeit-Zeile bestimmen (ItemKey + Einheit). Bei Kanalroboter weicht der
            // Hauptarbeit-ItemKey von der Massnahmen-Id ab (HAUPTARBEIT_HINDERNISSE_ROBOTER).
            var hauptLine = tpl.Lines.FirstOrDefault(l =>
                string.Equals(l.Group, "Hauptarbeit", StringComparison.OrdinalIgnoreCase));
            var hauptKey = string.IsNullOrWhiteSpace(hauptLine?.ItemKey) ? id : hauptLine!.ItemKey.Trim();
            catalog.TryGetValue(hauptKey, out var hauptItem);
            var unit = hauptItem?.Unit ?? "";

            // Manuelle Menge bei Stueck oder Stunden; Laengeneinheiten verwenden die Haltungslaenge.
            var manuelleMenge = UnitKinds.IsPiece(unit) || UnitKinds.IsHour(unit);
            var baseName = string.IsNullOrWhiteSpace(tpl.Name) ? id : tpl.Name;

            // Name ohne Praefix - die Kategorie zeigt der ComboBox-Gruppen-Header.
            options.Add(new MeasureOption(id, baseName, kategorie, manuelleMenge, hauptKey));
        }

        foreach (var o in options
                     .OrderBy(o => o.Kategorie == "Renovierung" ? 0 : o.Kategorie == "Reparatur" ? 1 : 2)
                     .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
        {
            result.Add(o);
        }

        return result;
    }
}
