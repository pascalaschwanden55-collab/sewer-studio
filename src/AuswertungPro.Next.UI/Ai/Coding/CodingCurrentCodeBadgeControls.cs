using System;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingCurrentCodeBadgeControls
{
    public static void Apply(
        Border badge,
        TextBlock text,
        CodingCurrentCodeBadgeState state)
    {
        ArgumentNullException.ThrowIfNull(badge);
        ArgumentNullException.ThrowIfNull(text);

        text.Text = state.Text;
        badge.Visibility = state.IsVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
