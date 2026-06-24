using System;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingOsdBadgeControls
{
    public static void Show(
        FrameworkElement badge,
        TextBlock textBlock,
        string text)
    {
        ArgumentNullException.ThrowIfNull(badge);
        ArgumentNullException.ThrowIfNull(textBlock);

        textBlock.Text = text;
        badge.Visibility = Visibility.Visible;
    }

    public static void ShowInitial(FrameworkElement badge, TextBlock textBlock)
        => Show(badge, textBlock, "OSD: --");

    public static void ShowMeter(
        FrameworkElement badge,
        TextBlock textBlock,
        double meter)
        => Show(badge, textBlock, CodingOsdBadgeDisplayPolicy.BuildMeterText(meter));

    public static void Hide(FrameworkElement badge)
    {
        ArgumentNullException.ThrowIfNull(badge);
        badge.Visibility = Visibility.Collapsed;
    }
}
