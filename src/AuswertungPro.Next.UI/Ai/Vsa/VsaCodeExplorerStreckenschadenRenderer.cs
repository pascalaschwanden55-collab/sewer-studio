using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public sealed record VsaCodeExplorerStreckenschadenRenderTargets(
    FrameworkElement TypPanel,
    ListBox TypList);

public static class VsaCodeExplorerStreckenschadenRenderer
{
    public static void Apply(
        VsaCodeExplorerStreckenschadenPresentation presentation,
        VsaCodeExplorerStreckenschadenRenderTargets targets)
    {
        targets.TypPanel.Visibility = presentation.ShowTypPanel
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (presentation.SelectedTypIndex is { } selectedIndex)
            targets.TypList.SelectedIndex = selectedIndex;
    }
}
