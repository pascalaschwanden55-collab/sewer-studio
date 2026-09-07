using System.Windows;
using System.Windows.Controls;

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
    public static DataPageColumnSetupResult Apply(DataGridColumn column, string fieldName, bool farbzelle = true)
    {
        ArgumentNullException.ThrowIfNull(column);

        column.SetValue(FrameworkElement.TagProperty, fieldName);

        var colorStyle = farbzelle ? DataGridColorCellStyleFactory.CreateHaltungenStyle(fieldName) : null;
        if (colorStyle is not null)
            column.CellStyle = colorStyle;

        column.CellStyle = DataGridFieldMetaTooltipStyleFactory.Create(fieldName, column.CellStyle);
        column.CanUserResize = true;
        column.MinWidth = fieldName == "NR" ? 56 : 72;

        var defaultHorizontalAlignment = string.Equals(fieldName, "Kosten", StringComparison.Ordinal)
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;

        return new DataPageColumnSetupResult(defaultHorizontalAlignment, VerticalAlignment.Center);
    }
}
