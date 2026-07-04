using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerQuantPanelRenderTargets(
    UIElement NoQuantText,
    UIElement Q1Panel,
    TextBlock Q1Label,
    TextBlock Q1Unit,
    TextBlock Q1Range,
    Border Q1RequiredBadge,
    UIElement Q2Panel,
    TextBlock Q2Label,
    TextBlock Q2Unit);

public sealed record VsaCodeExplorerQuantPanelRenderBrushes(
    Color DangerColor,
    Brush DangerBrush);

public static class VsaCodeExplorerQuantPanelRenderer
{
    public static void Apply(
        VsaCodeExplorerQuantPanelPresentation presentation,
        VsaCodeExplorerQuantPanelRenderTargets targets,
        VsaCodeExplorerQuantPanelRenderBrushes brushes)
    {
        targets.NoQuantText.Visibility = presentation.ShowNoQuant ? Visibility.Visible : Visibility.Collapsed;
        ApplyField(
            presentation.Q1,
            targets.Q1Panel,
            targets.Q1Label,
            targets.Q1Unit,
            targets.Q1Range,
            targets.Q1RequiredBadge,
            brushes);
        ApplyField(
            presentation.Q2,
            targets.Q2Panel,
            targets.Q2Label,
            targets.Q2Unit,
            rangeTextBlock: null,
            requiredBadge: null,
            brushes);
    }

    private static void ApplyField(
        VsaCodeExplorerQuantFieldPresentation field,
        UIElement panel,
        TextBlock labelTextBlock,
        TextBlock unitTextBlock,
        TextBlock? rangeTextBlock,
        Border? requiredBadge,
        VsaCodeExplorerQuantPanelRenderBrushes brushes)
    {
        panel.Visibility = field.ShowPanel ? Visibility.Visible : Visibility.Collapsed;
        if (!field.ShowPanel)
            return;

        labelTextBlock.Text = field.LabelText;
        unitTextBlock.Text = field.UnitText;

        if (rangeTextBlock is not null)
            rangeTextBlock.Text = field.RangeText;

        if (requiredBadge is null)
            return;

        var badge = field.RequiredBadge;
        requiredBadge.Visibility = badge is not null ? Visibility.Visible : Visibility.Collapsed;
        if (badge is null)
            return;

        ApplyRequiredBadge(requiredBadge, badge, brushes);
    }

    private static void ApplyRequiredBadge(
        Border badgeBorder,
        VsaCodeExplorerQuantRequiredBadgePresentation badge,
        VsaCodeExplorerQuantPanelRenderBrushes brushes)
    {
        badgeBorder.Background = new SolidColorBrush(ResolveBrushColor(badge.BrushRole, brushes))
        {
            Opacity = badge.BackgroundOpacity
        };

        if (badgeBorder.Child is TextBlock label)
        {
            label.Text = badge.Text;
            label.Foreground = ResolveBrush(badge.BrushRole, brushes);
        }
    }

    private static Color ResolveBrushColor(
        VsaCodeExplorerQuantBrushRole role,
        VsaCodeExplorerQuantPanelRenderBrushes brushes)
    {
        return role switch
        {
            VsaCodeExplorerQuantBrushRole.Danger => brushes.DangerColor,
            _ => brushes.DangerColor
        };
    }

    private static Brush ResolveBrush(
        VsaCodeExplorerQuantBrushRole role,
        VsaCodeExplorerQuantPanelRenderBrushes brushes)
    {
        return role switch
        {
            VsaCodeExplorerQuantBrushRole.Danger => brushes.DangerBrush,
            _ => brushes.DangerBrush
        };
    }
}
