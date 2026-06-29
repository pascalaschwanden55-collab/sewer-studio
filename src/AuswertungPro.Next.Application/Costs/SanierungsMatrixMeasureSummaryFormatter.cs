using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Costs;

/// <summary>
/// Zeilen-Detailansicht (read-only) fuer eine einzelne Kostenzeile in der Sanierungs-Matrix.
/// </summary>
public sealed record SanierungMatrixDetailLineVm(
    string Group,
    string Text,
    string Unit,
    decimal Qty,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>
/// Massnahmen-Detailansicht (read-only) fuer die Sanierungs-Matrix.
/// </summary>
public sealed record SanierungMatrixDetailMeasureVm(
    string MeasureName,
    string MeasureId,
    decimal Total,
    IReadOnlyList<SanierungMatrixDetailLineVm> Lines);

/// <summary>
/// Formatiert Massnahmen-Zusammenfassungen fuer die Sanierungs-Matrix.
/// Reine Logik ohne WPF-Abhaengigkeiten.
/// </summary>
public static class SanierungsMatrixMeasureSummaryFormatter
{
    public const string EmptySummary = "- keine -";

    public static string FormatSummary(HoldingCost? cost)
    {
        var names = MeasureNames(cost).ToList();
        return names.Count switch
        {
            0 => EmptySummary,
            1 => names[0],
            2 => $"{names[0]} + {names[1]}",
            _ => $"{names[0]} + {names[1]} + {names.Count - 2} weitere",
        };
    }

    public static IReadOnlyList<SanierungMatrixDetailMeasureVm> BuildDetailMeasures(HoldingCost? cost)
    {
        if (cost?.Measures is null || cost.Measures.Count == 0)
            return Array.Empty<SanierungMatrixDetailMeasureVm>();

        return cost.Measures
            .Select(m => new SanierungMatrixDetailMeasureVm(
                CleanMeasureName(m),
                m.MeasureId,
                m.Total,
                m.Lines
                    .Where(l => l.Selected)
                    .Select(l => new SanierungMatrixDetailLineVm(
                        l.Group,
                        l.Text,
                        l.Unit,
                        l.Qty,
                        l.UnitPrice,
                        l.Qty * l.UnitPrice))
                    .ToList()))
            .ToList();
    }

    private static IEnumerable<string> MeasureNames(HoldingCost? cost)
    {
        if (cost?.Measures is null)
            yield break;

        foreach (var measure in cost.Measures)
            yield return CleanMeasureName(measure);
    }

    private static string CleanMeasureName(MeasureCost measure)
    {
        if (!string.IsNullOrWhiteSpace(measure.MeasureName))
            return measure.MeasureName.Trim();

        if (!string.IsNullOrWhiteSpace(measure.MeasureId))
            return measure.MeasureId.Trim();

        return "Massnahme";
    }
}
