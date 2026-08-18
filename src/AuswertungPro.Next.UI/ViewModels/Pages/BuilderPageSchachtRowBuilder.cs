using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Baut die Druckcenter-Anzeigezeilen fuer Schaechte. Zwilling zum
/// <see cref="BuilderPageRowBuilder"/>: gleiche Zeilenform, damit Filter, Statistik und
/// PDF-Ausgabe unveraendert weiterlaufen. Zustandslos und ohne WPF testbar.
///
/// Unterschied zur Haltung: Es gibt ZWEI gepflegte Schacht-Kostenquellen —
/// die Schacht-Matrix (schacht_costs.json) und der Massnahmen-Dialog der Schaechte-Seite
/// (schacht_empfehlungen.json). Die Matrix hat Vorrang, die Empfehlung ist der Rueckfall;
/// zusammengezaehlt wird NIE, sonst stuende derselbe Schacht doppelt im Ausdruck.
/// Einen Tabellenwert-Rueckfall gibt es bewusst nicht.
/// </summary>
public static class BuilderPageSchachtRowBuilder
{
    public const string UnknownOwnerLabel = BuilderPageRowBuilder.UnknownOwnerLabel;

    // Feldnamen weichen je nach Importquelle (WinCan/XTF/KINS) ab; darum mehrere Kandidaten.
    private static readonly string[] OwnerFields = ["Eigentuemer", "Eigentümer"];
    private static readonly string[] StreetFields = ["Strasse", "Strassenname", "Standortname"];
    private static readonly string[] ExecutedByFields = ["Ausgefuehrt_durch", "Ausgeführt durch"];
    private static readonly string[] SanierenFields = ["Sanieren", "Sanieren_JaNein"];
    private static readonly string[] MaterialFields = ["Material", "Schachtmaterial"];
    private static readonly string[] StatusFields = ["Status", "Funktion"];
    private static readonly string[] ZustandFields = ["Zustandsklasse", "Pruefungsresultat"];
    private static readonly string[] YearFields = ["Inspektionsjahr", "Inspektionsdatum", "Untersuchungsdatum"];

    /// <param name="schachtCostStore">Kosten der Schacht-Matrix (schacht_costs.json).</param>
    /// <param name="empfehlungCostStore">
    /// Kosten des Schacht-Massnahmen-Dialogs (schacht_empfehlungen.json). Rueckfall, wenn
    /// die Matrix fuer diesen Schacht nichts kennt.
    /// </param>
    public static List<DruckcenterRowVm> Build(
        IEnumerable<SchachtRecord> records,
        IReadOnlyDictionary<string, string> projectMetadata,
        ProjectCostStore schachtCostStore,
        ProjectCostStore? empfehlungCostStore = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(projectMetadata);
        ArgumentNullException.ThrowIfNull(schachtCostStore);

        var rows = new List<DruckcenterRowVm>();
        foreach (var record in records)
        {
            var nummer = SchaechteColumnPolicy.GetSchachtNumber(record);
            if (nummer.Length == 0)
                nummer = "(ohne Schachtnummer)";

            var owner = FirstValue(record, OwnerFields);
            if (owner.Length == 0 && projectMetadata.TryGetValue(FieldKeys.Owner, out var ownerMeta))
                owner = SafeText(ownerMeta);
            if (owner.Length == 0)
                owner = UnknownOwnerLabel;

            var executedBy = FirstValue(record, ExecutedByFields);
            if (executedBy.Length == 0)
                executedBy = "(unbekannt)";

            var material = FirstValue(record, MaterialFields);
            if (material.Length == 0)
                material = "(unbekannt)";

            // Matrix zuerst; nur wenn sie fuer diesen Schacht nichts hat, gilt die Empfehlung.
            var storedCost = TryGetCostBySchacht(schachtCostStore, nummer);
            var ausEmpfehlung = false;
            if (storedCost is null && empfehlungCostStore is not null)
            {
                storedCost = TryGetCostBySchacht(empfehlungCostStore, nummer);
                ausEmpfehlung = storedCost is not null;
            }

            var hasDetailedCost = TablePauschaleCostHelper.HasDetailedCost(storedCost);
            var netCost = storedCost is null ? 0m : TablePauschaleCostHelper.ResolveNetTotal(storedCost);
            if (netCost < 0m)
                netCost = 0m;

            var measures = MeasureNames(storedCost);
            var measuresPreview = BuildMeasurePreview(measures);

            rows.Add(new DruckcenterRowVm
            {
                Kind = DruckcenterRowKind.Schacht,
                Record = null,
                Holding = nummer,
                Street = FirstValue(record, StreetFields),
                Owner = owner,
                Sanieren = FirstValue(record, SanierenFields),
                ExecutedBy = executedBy,
                Material = material,
                Status = FirstValue(record, StatusFields),
                Year = NormalizeYear(FirstValue(record, YearFields)),
                Zustand = FirstValue(record, ZustandFields),
                NetCost = netCost,
                StoredCost = storedCost,
                HasDetailedCost = hasDetailedCost,
                HasMeasures = measures.Count > 0,
                // Die Quelle muss im Ausdruck sichtbar bleiben — sonst weiss niemand,
                // ob eine Zahl aus der Matrix oder aus dem Massnahmen-Dialog stammt.
                CostSource = ausEmpfehlung
                    ? "Schacht-Massnahmen"
                    : hasDetailedCost
                        ? "Positionsdetails"
                        : netCost > 0m
                            ? "Kostenstore"
                            : "Keine Kosten",
                MeasuresRaw = string.Join("; ", measures),
                MeasuresPreview = measuresPreview
            });
        }

        return rows
            .OrderBy(row => string.IsNullOrWhiteSpace(row.ExecutedBy) ? 1 : 0)
            .ThenBy(row => row.ExecutedBy, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Owner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Holding, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HoldingCost? TryGetCostBySchacht(ProjectCostStore costStore, string nummer)
    {
        if (string.IsNullOrWhiteSpace(nummer))
            return null;
        if (costStore.ByHolding.TryGetValue(nummer, out var direct))
            return direct;

        foreach (var entry in costStore.ByHolding)
        {
            if (string.Equals(entry.Key, nummer, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }

        return null;
    }

    /// <summary>Namen der in der Schacht-Matrix gewaehlten Massnahmen, ohne Leereintraege.</summary>
    private static List<string> MeasureNames(HoldingCost? cost)
        => cost is null
            ? []
            : cost.Measures
                .Select(measure => SafeText(measure.MeasureName))
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static string BuildMeasurePreview(IReadOnlyList<string> entries)
        => entries.Count switch
        {
            0 => string.Empty,
            1 => entries[0],
            2 => $"{entries[0]}; {entries[1]}",
            _ => $"{entries[0]}; {entries[1]} (+{entries.Count - 2} weitere)"
        };

    private static string NormalizeYear(string value)
    {
        if (value.Length >= 4
            && int.TryParse(value[..4], out var year)
            && year is >= 1900 and <= 2200)
        {
            return year.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return value;
    }

    /// <summary>Erster nichtleerer Wert aus den Kandidatenfeldern.</summary>
    private static string FirstValue(SchachtRecord record, string[] fieldNames)
    {
        foreach (var name in fieldNames)
        {
            var value = SafeText(record.GetFieldValue(name));
            if (value.Length > 0)
                return value;
        }

        return string.Empty;
    }

    private static string SafeText(string? value)
        => (value ?? string.Empty).Trim();
}
