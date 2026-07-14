using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Einheitliches Icon-Element fuer Menues, Schaltflaechen und Ueberschriften.
/// </summary>
public sealed class FluentIcon : TextBlock
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(FluentIcon),
        new PropertyMetadata(
            string.Empty,
            static (element, args) =>
                ((FluentIcon)element).Text = args.NewValue as string ?? string.Empty));

    public FluentIcon()
    {
        FontFamily = IconFonts.Default;
        FontSize = 13;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }
}
