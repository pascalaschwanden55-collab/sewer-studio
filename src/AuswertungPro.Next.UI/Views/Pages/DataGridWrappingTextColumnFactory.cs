using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataGridWrappingTextColumnFactory
{
    public static DataGridTextColumn Create(string fieldName, string header)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding($"Fields[{fieldName}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            },
            ElementStyle = CreateDisplayStyle(),
            EditingElementStyle = CreateEditStyle(),
            Width = DataGridLength.SizeToHeader
        };
    }

    private static Style CreateDisplayStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private static Style CreateEditStyle()
    {
        var style = new Style(typeof(TextBox));
        style.Setters.Add(new Setter(TextBox.TextWrappingProperty, TextWrapping.Wrap));
        style.Setters.Add(new Setter(TextBox.AcceptsReturnProperty, true));
        style.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Top));
        style.Setters.Add(new Setter(TextBox.MinHeightProperty, 60d));
        return style;
    }
}
