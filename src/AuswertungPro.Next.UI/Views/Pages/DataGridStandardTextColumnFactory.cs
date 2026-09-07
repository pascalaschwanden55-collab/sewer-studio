using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;
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
        if (DataPageColumnStyleRules.IstNamensspalte(fieldName))
            displayStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        if (DataPageColumnStyleRules.IstZahlenspalte(fieldName))
        {
            displayStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, System.Windows.Application.Current?.TryFindResource("FontMono") ?? new FontFamily("Consolas")));
            displayStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        }
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
