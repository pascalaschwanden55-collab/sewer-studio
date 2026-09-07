using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Theme;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataGridWrappingTextColumnFactory
{
    public static DataGridTextColumn Create(string fieldName, string header, bool hoeheBegrenzen = false)
        => Create(fieldName, header, ApplicationStyleResolver.FindImplicit, hoeheBegrenzen);

    /// <param name="hoeheBegrenzen">
    /// Nova-Etappe 2b: hoechstens drei Zeilen je Zelle. Gilt nur im Nova-Layout; die alte
    /// Haltungsansicht bleibt unveraendert (Fix-Runde 1, F4).
    /// </param>
    internal static DataGridTextColumn Create(
        string fieldName,
        string header,
        Func<Type, Style?> implicitStyleResolver,
        bool hoeheBegrenzen = false)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding($"Fields[{fieldName}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            },
            ElementStyle = CreateDisplayStyle(implicitStyleResolver(typeof(TextBlock)), fieldName, hoeheBegrenzen),
            EditingElementStyle = CreateEditStyle(implicitStyleResolver(typeof(TextBox))),
            Width = DataGridLength.SizeToHeader
        };
    }

    private static Style CreateDisplayStyle(Style? baseStyle, string fieldName, bool hoeheBegrenzen)
    {
        var style = new Style(typeof(TextBlock), baseStyle);
        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new Binding("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        }));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        // Nova-Etappe 2b: hoechstens drei Zeilen je Zelle. Ein Text mit Zeilenumbruechen zog
        // sonst die ganze Tabellenzeile auf (Bild 07.09.). Den Volltext traegt der Hinweis der
        // Zelle (DataGridFieldMetaTooltipStyleFactory) zusammen mit der Herkunftszeile.
        if (hoeheBegrenzen)
            style.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty, DataPageColumnStyleRules.MaximaleZellenhoehe));
        if (DataPageColumnStyleRules.IstNamensspalte(fieldName))
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        if (DataPageColumnStyleRules.IstZahlenspalte(fieldName))
        {
            style.Setters.Add(new Setter(TextBlock.FontFamilyProperty, System.Windows.Application.Current?.TryFindResource("FontMono") ?? new FontFamily("Consolas")));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        }
        return style;
    }

    private static Style CreateEditStyle(Style? baseStyle)
    {
        var style = new Style(typeof(TextBox), baseStyle);
        style.Setters.Add(new Setter(TextBox.TextWrappingProperty, TextWrapping.Wrap));
        style.Setters.Add(new Setter(TextBox.AcceptsReturnProperty, true));
        style.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Top));
        style.Setters.Add(new Setter(TextBox.MinHeightProperty, 60d));
        return style;
    }
}
