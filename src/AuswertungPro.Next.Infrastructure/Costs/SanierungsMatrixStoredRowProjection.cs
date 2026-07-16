using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Fertiger Anzeigezustand einer Matrixzeile aus den gespeicherten Projektkosten.
/// </summary>
public sealed record SanierungsMatrixStoredRowState(
    HoldingCost? StoredCost,
    MeasureOption SelectedMeasure,
    decimal Total,
    decimal Menge,
    bool Verkehrsdienst,
    bool Wasserhaltung,
    bool Fraesen,
    bool Dichtheitspruefung,
    bool Dokumentation,
    bool HasMultipleMeasures,
    MeasureOption? AdditionalOption);

/// <summary>
/// Projiziert einen costs.json-Eintrag auf den Anzeigezustand einer Matrixzeile.
/// Die Logik ist unabhaengig von WPF und veraendert weder Store noch Optionen.
/// </summary>
public static class SanierungsMatrixStoredRowProjection
{
    public static SanierungsMatrixStoredRowState Project(
        ProjectCostStore store,
        string holding,
        IReadOnlyList<MeasureOption> measureOptions)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(measureOptions);

        var emptyOption = measureOptions.FirstOrDefault(option => option.Id is null)
            ?? throw new ArgumentException(
                "Die Auswahlliste enthaelt keine Option fuer 'keine Massnahme'.",
                nameof(measureOptions));

        if (string.IsNullOrWhiteSpace(holding)
            || !store.ByHolding.TryGetValue(holding, out var existing)
            || existing.Measures.Count == 0)
        {
            return Empty(emptyOption);
        }

        var firstMeasure = existing.Measures[0];
        var selectedOption = measureOptions.FirstOrDefault(option => string.Equals(
            option.Id,
            firstMeasure.MeasureId,
            StringComparison.OrdinalIgnoreCase));
        var hasMultipleMeasures = existing.Measures.Count > 1;

        if (selectedOption is null)
        {
            var name = string.IsNullOrWhiteSpace(firstMeasure.MeasureName)
                ? firstMeasure.MeasureId
                : firstMeasure.MeasureName;
            var additionalOption = new MeasureOption(
                firstMeasure.MeasureId,
                name + " (gespeichert)",
                "Übrige",
                false,
                firstMeasure.MeasureId);

            return new SanierungsMatrixStoredRowState(
                existing,
                additionalOption,
                existing.Total,
                0m,
                false,
                false,
                false,
                false,
                false,
                hasMultipleMeasures,
                additionalOption);
        }

        var lines = firstMeasure.Lines;
        var mainLine = lines.FirstOrDefault(line => string.Equals(
            line.ItemKey,
            selectedOption.HauptItemKey,
            StringComparison.OrdinalIgnoreCase));

        return new SanierungsMatrixStoredRowState(
            existing,
            selectedOption,
            existing.Total,
            mainLine?.Qty ?? 0m,
            IsSelected(lines, SanierungsMatrixOptionKeys.Verkehrsdienst),
            IsSelected(lines, SanierungsMatrixOptionKeys.Wasserhaltung),
            IsSelected(lines, SanierungsMatrixOptionKeys.Fraesen),
            IsSelected(lines, SanierungsMatrixOptionKeys.Dichtheitspruefung),
            IsSelected(lines, SanierungsMatrixOptionKeys.Dokumentation),
            hasMultipleMeasures,
            null);
    }

    private static SanierungsMatrixStoredRowState Empty(MeasureOption emptyOption)
        => new(
            null,
            emptyOption,
            0m,
            0m,
            false,
            false,
            false,
            false,
            false,
            false,
            null);

    private static bool IsSelected(IEnumerable<CostLine> lines, string itemKey)
        => lines.Any(line => line.Selected && string.Equals(
            line.ItemKey,
            itemKey,
            StringComparison.OrdinalIgnoreCase));
}
