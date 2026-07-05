using System.Collections.Generic;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>Auswahl fuer den Kosten-PDF-Export: welche Zeilen und welcher Variantentitel.</summary>
public sealed record BuilderPageExportSelection(
    IReadOnlyList<DruckcenterRowVm> Rows,
    string VariantTitle);

/// <summary>
/// Bestimmt Umfang und Titel des Druckcenter-Kostenausdrucks:
/// alle (gefilterten) Haltungen oder genau eine einzelne.
/// </summary>
public static class BuilderPageExportScope
{
    /// <summary>Alle (gefilterten) Haltungen — Titel wie der bisherige Gesamtausdruck.</summary>
    public static BuilderPageExportSelection All(IReadOnlyList<DruckcenterRowVm> filteredRows)
        => new(filteredRows, $"Gefilterte Kostenzusammenstellung ({filteredRows.Count} Haltungen)");

    /// <summary>Genau eine Haltung — Titel mit Haltungsname, neutraler Fallback ohne Namen.</summary>
    public static BuilderPageExportSelection Single(DruckcenterRowVm row)
        => new(new[] { row }, SingleVariantTitle(row.Holding));

    private static string SingleVariantTitle(string? holding)
    {
        var name = (holding ?? string.Empty).Trim();
        return name.Length == 0
            ? "Kostenzusammenstellung - einzelne Haltung"
            : $"Kostenzusammenstellung - Haltung {name}";
    }
}
