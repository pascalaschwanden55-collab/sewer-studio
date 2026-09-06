using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.Theme;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataGridStandardTextColumnFactory
{
    public static DataGridTextColumn Create(
        string fieldName,
        string header,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.LostFocus)
    {
        var displayStyle = new Style(typeof(TextBlock), ApplicationStyleResolver.FindImplicit(typeof(TextBlock)));
        displayStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new Binding("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        }));
        return new DataGridTextColumn
        {
            Header = header,
            ElementStyle = displayStyle,
            Binding = new Binding($"Fields[{fieldName}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = updateSourceTrigger
            },
            Width = DataGridLength.SizeToHeader
        };
    }
}
