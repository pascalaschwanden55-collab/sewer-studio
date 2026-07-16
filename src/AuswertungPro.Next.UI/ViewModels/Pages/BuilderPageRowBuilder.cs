using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Baut die Anzeigezeilen des Druckcenters aus Projekt- und Kostendaten.
/// Die Klasse ist zustandslos und kann ohne WPF-Oberflaeche getestet werden.
/// </summary>
public static class BuilderPageRowBuilder
{
    public const string UnknownOwnerLabel = "Unbekannt";

    public static List<DruckcenterRowVm> Build(
        IEnumerable<HaltungRecord> records,
        IReadOnlyDictionary<string, string> projectMetadata,
        ProjectCostStore costStore)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(projectMetadata);
        ArgumentNullException.ThrowIfNull(costStore);

        var rows = new List<DruckcenterRowVm>();
        foreach (var record in records)
        {
            var holding = SafeText(record.GetFieldValue(FieldKeys.HoldingName));
            if (holding.Length == 0)
                holding = "(ohne Haltungsname)";

            var owner = SafeText(record.GetFieldValue(FieldKeys.Owner));
            if (owner.Length == 0 && projectMetadata.TryGetValue(FieldKeys.Owner, out var ownerMeta))
                owner = SafeText(ownerMeta);
            if (owner.Length == 0)
                owner = UnknownOwnerLabel;

            var executedBy = SafeText(record.GetFieldValue(FieldKeys.RehabilitationExecutor));
            if (executedBy.Length == 0)
                executedBy = "(unbekannt)";

            var material = SafeText(record.GetFieldValue(FieldKeys.PipeMaterial));
            if (material.Length == 0)
                material = "(unbekannt)";

            var recommendedRaw = record.GetFieldValue(FieldKeys.RecommendedRehabilitationMeasures);
            var recommendedPreview = BuildMeasurePreview(recommendedRaw);
            var tableCost = TablePauschaleCostHelper.ParseTableNetCost(
                record.GetFieldValue(FieldKeys.Cost));
            var storedCost = TryGetCostByHolding(costStore, holding);
            var hasDetailedCost = TablePauschaleCostHelper.HasDetailedCost(storedCost);
            var netCost = storedCost is null
                ? tableCost
                : TablePauschaleCostHelper.ResolveNetTotal(storedCost);

            // Ein leerer Kostenstore darf einen manuell gepflegten Tabellenwert
            // nicht verdraengen.
            if (netCost <= 0m && tableCost > 0m)
                netCost = tableCost;
            if (netCost < 0m)
                netCost = 0m;

            rows.Add(new DruckcenterRowVm
            {
                Record = record,
                Holding = holding,
                Street = SafeText(record.GetFieldValue(FieldKeys.Street)),
                Owner = owner,
                Sanieren = SafeText(record.GetFieldValue(FieldKeys.RenovationDecision)),
                ExecutedBy = executedBy,
                Material = material,
                Status = SafeText(record.GetFieldValue(FieldKeys.WorkflowStatus)),
                Year = NormalizeYear(record.GetFieldValue(FieldKeys.InspectionYear)),
                Zustand = SafeText(record.GetFieldValue(FieldKeys.ConditionClass)),
                NetCost = netCost,
                StoredCost = storedCost,
                HasDetailedCost = hasDetailedCost,
                HasMeasures = hasDetailedCost || recommendedPreview.Length > 0,
                CostSource = hasDetailedCost
                    ? "Positionsdetails"
                    : netCost > 0m
                        ? (storedCost is null ? "Tabellenwert" : "Kostenstore")
                        : "Keine Kosten",
                MeasuresRaw = recommendedRaw ?? string.Empty,
                MeasuresPreview = recommendedPreview
            });
        }

        return rows
            .OrderBy(row => string.IsNullOrWhiteSpace(row.ExecutedBy) ? 1 : 0)
            .ThenBy(row => row.ExecutedBy, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Owner, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Holding, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HoldingCost? TryGetCostByHolding(ProjectCostStore costStore, string holding)
    {
        if (string.IsNullOrWhiteSpace(holding))
            return null;
        if (costStore.ByHolding.TryGetValue(holding, out var direct))
            return direct;

        foreach (var entry in costStore.ByHolding)
        {
            if (string.Equals(entry.Key, holding, StringComparison.OrdinalIgnoreCase))
                return entry.Value;
        }

        return null;
    }

    private static string NormalizeYear(string? value)
    {
        var text = SafeText(value);
        if (text.Length >= 4
            && int.TryParse(text[..4], out var year)
            && year is >= 1900 and <= 2200)
        {
            return year.ToString(CultureInfo.InvariantCulture);
        }

        return text;
    }

    private static string BuildMeasurePreview(string? raw)
    {
        var entries = ParseMeasureEntries(raw);
        return entries.Count switch
        {
            0 => string.Empty,
            1 => entries[0],
            2 => $"{entries[0]}; {entries[1]}",
            _ => $"{entries[0]}; {entries[1]} (+{entries.Count - 2} weitere)"
        };
    }

    private static List<string> ParseMeasureEntries(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        return raw
            .Split(['\r', '\n', ';', ',', '|'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeMeasureEntry)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeMeasureEntry(string? value)
    {
        var text = SafeText(value);
        while (text.Length > 0 && text[0] is '-' or '*')
            text = text[1..].TrimStart();
        return text;
    }

    private static string SafeText(string? value)
        => (value ?? string.Empty).Trim();
}
