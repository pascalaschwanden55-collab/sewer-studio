using System.Collections.Generic;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>Auswahl fuer den Kosten-PDF-Export: welche Zeilen und welcher Variantentitel.</summary>
public sealed record BuilderPageExportSelection(
    IReadOnlyList<DruckcenterRowVm> Rows,
    string VariantTitle);

/// <summary>
/// Bestimmt Umfang und Titel des Druckcenter-Kostenausdrucks:
/// alle (gefilterten) Bauteile eines Bereichs oder genau eines.
/// Haltungen und Schaechte werden getrennt gedruckt — die Bauteilart steht darum im Titel.
/// </summary>
public static class BuilderPageExportScope
{
    /// <summary>Alle (gefilterten) Bauteile des gewaehlten Bereichs.</summary>
    public static BuilderPageExportSelection All(
        IReadOnlyList<DruckcenterRowVm> filteredRows,
        string bauteilLabelPlural = "Haltungen")
        => new(
            filteredRows,
            $"Gefilterte Kostenzusammenstellung ({filteredRows.Count} {Safe(bauteilLabelPlural, "Haltungen")})");

    /// <summary>Genau ein Bauteil — Titel mit Namen, neutraler Fallback ohne Namen.</summary>
    public static BuilderPageExportSelection Single(
        DruckcenterRowVm row,
        string bauteilLabel = "Haltung")
        => new(new[] { row }, SingleVariantTitle(row.Holding, Safe(bauteilLabel, "Haltung")));

    private static string SingleVariantTitle(string? holding, string bauteilLabel)
    {
        var name = (holding ?? string.Empty).Trim();
        return name.Length == 0
            ? $"Kostenzusammenstellung - {bauteilLabel} ohne Bezeichnung"
            : $"Kostenzusammenstellung - {bauteilLabel} {name}";
    }

    private static string Safe(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }
}
