using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerProgressRenderTargets(
    IReadOnlyList<Border> Bars,
    IReadOnlyList<TextBlock> Labels,
    TextBlock CodePreview);

public sealed record VsaCodeExplorerProgressRenderBrushes(
    Color SuccessColor,
    Color GroupColor,
    Color BorderLightColor,
    Brush TextSecondaryBrush,
    Brush MutedBrush);

public static class VsaCodeExplorerProgressRenderer
{
    public static void Apply(
        VsaCodeExplorerProgressPresentation presentation,
        VsaCodeExplorerProgressRenderTargets targets,
        VsaCodeExplorerProgressRenderBrushes brushes)
    {
        for (var i = 0; i < presentation.Segments.Count; i++)
        {
            var segment = presentation.Segments[i];
            var barColor = ResolveProgressBarColor(
                segment.BarRole,
                brushes.GroupColor,
                brushes.SuccessColor,
                brushes.BorderLightColor);

            targets.Bars[i].Background = new SolidColorBrush(barColor);
            targets.Labels[i].FontWeight = segment.LabelBold ? FontWeights.Bold : FontWeights.Normal;
            targets.Labels[i].Foreground = segment.LabelRole == VsaCodeExplorerProgressLabelRole.Secondary
                ? brushes.TextSecondaryBrush
                : brushes.MutedBrush;
        }

        targets.CodePreview.Text = presentation.CodePreviewText;
    }

    private static Color ResolveProgressBarColor(
        VsaCodeExplorerProgressBarRole role,
        Color groupColor,
        Color successColor,
        Color borderLightColor)
    {
        return role switch
        {
            VsaCodeExplorerProgressBarRole.Success => successColor,
            VsaCodeExplorerProgressBarRole.Group => groupColor,
            VsaCodeExplorerProgressBarRole.CurrentGroup => Color.FromArgb(0x80, groupColor.R, groupColor.G, groupColor.B),
            _ => borderLightColor
        };
    }
}
