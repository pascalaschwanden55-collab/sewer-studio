using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Gibt einem kleinen Werkzeugknopf ein Fluent-Glyph statt eines Textzeichens.
/// Der Tooltip des Knopfs wird zugleich als zugaenglicher Name gesetzt, weil das
/// Glyph selbst nicht vorgelesen werden kann.
/// </summary>
internal static class FluentGlyphKnopf
{
    public static Button MitGlyph(this Button knopf, string glyph, bool gespiegelt = false)
    {
        var symbol = new FluentIcon { Glyph = glyph, FontSize = 12 };
        if (gespiegelt)
        {
            symbol.RenderTransformOrigin = new Point(0.5, 0.5);
            symbol.RenderTransform = new ScaleTransform(-1, 1);
        }

        knopf.Content = symbol;
        if (knopf.ToolTip is string hinweis && !string.IsNullOrWhiteSpace(hinweis))
            AutomationProperties.SetName(knopf, hinweis);

        return knopf;
    }
}
