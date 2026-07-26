using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProtocolMatchHighlightControls
{
    public static void Clear(ListBoxItem container, Border? badge)
    {
        ArgumentNullException.ThrowIfNull(container);

        if (badge != null)
            badge.Visibility = Visibility.Collapsed;
        container.ClearValue(Control.BackgroundProperty);
        container.ClearValue(FrameworkElement.ToolTipProperty);
    }

    public static void Apply(
        ListBoxItem container,
        Border? badge,
        TextBlock? badgeText,
        CodingProtocolMatchBucket bucket)
    {
        ArgumentNullException.ThrowIfNull(container);

        container.Background = new SolidColorBrush(CodingProtocolMatchDisplayPolicy.BackgroundColor(bucket));
        container.ToolTip = CodingProtocolMatchDisplayPolicy.Tooltip(bucket);

        if (badge != null)
        {
            badge.Background = new SolidColorBrush(CodingProtocolMatchDisplayPolicy.BadgeColor(bucket));
            badge.Visibility = Visibility.Visible;
        }

        if (badgeText != null)
            badgeText.Text = CodingProtocolMatchDisplayPolicy.BadgeText(bucket);
    }
}
