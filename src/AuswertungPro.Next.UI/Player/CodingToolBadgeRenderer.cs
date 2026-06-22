using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Player;

public static class CodingToolBadgeRenderer
{
    public static void Update(Canvas canvas, string? toolText)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        Clear(canvas);
        if (string.IsNullOrWhiteSpace(toolText))
            return;

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4, 8, 4),
            Tag = OverlayTags.ToolBadge,
            Child = new TextBlock
            {
                Text = toolText,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF))
            }
        };

        Canvas.SetLeft(badge, 10);
        Canvas.SetTop(badge, 10);
        canvas.Children.Add(badge);
    }

    private static void Clear(Canvas canvas)
    {
        var old = canvas.Children
            .OfType<FrameworkElement>()
            .Where(e => e.Tag is string s && s == OverlayTags.ToolBadge)
            .ToList();

        foreach (var element in old)
            canvas.Children.Remove(element);
    }
}
