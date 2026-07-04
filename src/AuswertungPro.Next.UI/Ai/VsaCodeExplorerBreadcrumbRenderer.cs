using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerBreadcrumbRenderBrushes(
    Brush TextBrush,
    Brush MutedBrush);

public sealed record VsaCodeExplorerBreadcrumbRenderTargets(
    ItemsControl BreadcrumbPanel,
    Style ToolbarButtonStyle,
    VsaCodeExplorerBreadcrumbRenderBrushes Brushes,
    FontFamily FontFamily,
    Action<int> Navigate);

public static class VsaCodeExplorerBreadcrumbRenderer
{
    public static void Apply(
        VsaCodeExplorerBreadcrumbPresentation presentation,
        VsaCodeExplorerBreadcrumbRenderTargets targets)
    {
        targets.BreadcrumbPanel.Items.Clear();

        foreach (var element in presentation.Elements)
        {
            targets.BreadcrumbPanel.Items.Add(element.IsSeparator
                ? CreateSeparator(element, targets)
                : CreateButton(element, targets));
        }
    }

    private static TextBlock CreateSeparator(
        VsaCodeExplorerBreadcrumbElement element,
        VsaCodeExplorerBreadcrumbRenderTargets targets)
    {
        return new TextBlock
        {
            Text = element.Text,
            FontSize = 10,
            Foreground = targets.Brushes.MutedBrush,
            Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static Button CreateButton(
        VsaCodeExplorerBreadcrumbElement element,
        VsaCodeExplorerBreadcrumbRenderTargets targets)
    {
        var button = new Button
        {
            Content = element.Text,
            Style = targets.ToolbarButtonStyle,
            FontFamily = targets.FontFamily,
            FontSize = 11,
            FontWeight = element.IsCurrent ? FontWeights.SemiBold : FontWeights.Normal,
            Padding = new Thickness(3, 1, 3, 1),
            MinWidth = 0,
            MinHeight = 0,
            Foreground = element.IsCurrent ? targets.Brushes.TextBrush : targets.Brushes.MutedBrush
        };

        if (element.CanNavigate)
        {
            var level = element.Level;
            button.Click += (_, _) => targets.Navigate(level);
        }

        return button;
    }
}
