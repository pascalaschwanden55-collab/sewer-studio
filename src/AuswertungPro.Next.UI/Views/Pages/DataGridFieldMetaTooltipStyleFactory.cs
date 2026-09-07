using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataGridFieldMetaTooltipStyleFactory
{
    /// <param name="mitVolltext">
    /// Fix-Runde 1 (F4, Entscheid): Eine gekuerzte Zelle zeigt ihren Volltext oben im Hinweis
    /// und darunter die gewohnte Herkunftszeile. Der Volltext ersetzt die Herkunft NICHT —
    /// beide stehen im selben Hinweis.
    /// </param>
    public static Style Create(string fieldName, Style? baseStyle, bool mitVolltext = false)
    {
        var style = new Style(typeof(DataGridCell),
            baseStyle ?? Theme.ApplicationStyleResolver.FindImplicit(typeof(DataGridCell)));

        var herkunft = new TextBlock();
        var binding = new MultiBinding { StringFormat = "Quelle: {0} | UserEdited: {1} | Konflikt: {2}" };
        binding.Bindings.Add(new Binding($"FieldMeta[{fieldName}].Source"));
        binding.Bindings.Add(new Binding($"FieldMeta[{fieldName}].UserEdited"));
        binding.Bindings.Add(new Binding($"FieldMeta[{fieldName}].Conflict"));
        herkunft.SetBinding(TextBlock.TextProperty, binding);

        style.Setters.Add(new Setter(
            FrameworkElement.ToolTipProperty,
            mitVolltext ? MitVolltext(fieldName, herkunft) : herkunft));
        return style;
    }

    /// <summary>Volltext oben, Herkunftszeile darunter. Ohne Inhalt bleibt die obere Zeile weg.</summary>
    private static StackPanel MitVolltext(string fieldName, TextBlock herkunft)
    {
        var volltext = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
        volltext.SetBinding(TextBlock.TextProperty, new Binding($"Fields[{fieldName}]"));
        volltext.SetBinding(UIElement.VisibilityProperty, new Binding($"Fields[{fieldName}]")
        {
            Converter = LeerZuUnsichtbarConverter.Instance
        });

        herkunft.Margin = new Thickness(0, 4, 0, 0);
        herkunft.Opacity = 0.75;

        var inhalt = new StackPanel();
        inhalt.Children.Add(volltext);
        inhalt.Children.Add(herkunft);
        return inhalt;
    }
}

/// <summary>Leerer Text -> die Zeile verschwindet, statt eine leere Zeile im Hinweis zu lassen.</summary>
public sealed class LeerZuUnsichtbarConverter : System.Windows.Data.IValueConverter
{
    public static readonly LeerZuUnsichtbarConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => Binding.DoNothing;
}
