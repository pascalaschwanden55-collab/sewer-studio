using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Views.Pages;

public sealed record DataPageColumnSetupResult(
    HorizontalAlignment DefaultHorizontalAlignment,
    VerticalAlignment DefaultVerticalAlignment);

public static class DataPageColumnSetup
{
    public static DataPageColumnSetupResult Apply(DataGridColumn column, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(column);

        column.SetValue(FrameworkElement.TagProperty, fieldName);

        var colorStyle = DataGridColorCellStyleFactory.CreateHaltungenStyle(fieldName);
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
