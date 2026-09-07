using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

public sealed record DataPageColumnSetupResult(
    HorizontalAlignment DefaultHorizontalAlignment,
    VerticalAlignment DefaultVerticalAlignment);

public static class DataPageColumnSetup
{
    /// <param name="farbzelle">
    /// Nova-Etappe 2b: Im Nova-Layout traegt die Zustandsklasse eine Marke ("Chip") in der Zelle
    /// statt einer ganzflaechig eingefaerbten Zelle. Fuer diese Spalte wird die Farbzelle
    /// deshalb bewusst nicht gesetzt; die alte Haltungsansicht behaelt sie unveraendert.
    /// </param>
    /// <param name="volltextHinweis">
    /// Fix-Runde 1 (F4): Eine auf drei Zeilen gekuerzte Zelle zeigt ihren Volltext oben im
    /// Hinweis, die Herkunftszeile bleibt darunter erhalten.
    /// </param>
    public static DataPageColumnSetupResult Apply(
        DataGridColumn column,
        string fieldName,
        bool farbzelle = true,
        bool volltextHinweis = false)
    {
        ArgumentNullException.ThrowIfNull(column);

        column.SetValue(FrameworkElement.TagProperty, fieldName);

        var colorStyle = farbzelle ? DataGridColorCellStyleFactory.CreateHaltungenStyle(fieldName) : null;
        if (colorStyle is not null)
            column.CellStyle = colorStyle;

        column.CellStyle = DataGridFieldMetaTooltipStyleFactory.Create(fieldName, column.CellStyle, volltextHinweis);
        column.CanUserResize = true;
        column.MinWidth = fieldName == "NR" ? 56 : 72;

        // Nova-Fixwelle 2b (P1): Zahlen stehen rechts — und zwar ALLE Zahlenspalten, nicht nur
        // "Kosten". Die Spaltenfabrik setzt zwar TextAlignment.Right, aber der
        // DataGridColumnLayoutController schreibt danach die hier gelieferte Ausrichtung in
        // Zell- und Textstil und gewann damit gegen die Fabrik. Eine gespeicherte
        // Nutzerausrichtung behaelt Vorrang: Sie wird erst danach aus dem Layout gelesen.
        var defaultHorizontalAlignment = DataPageColumnStyleRules.IstZahlenspalte(fieldName)
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;

        return new DataPageColumnSetupResult(defaultHorizontalAlignment, VerticalAlignment.Center);
    }
}
