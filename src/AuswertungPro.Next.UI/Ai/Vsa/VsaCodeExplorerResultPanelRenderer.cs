using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public sealed record VsaCodeExplorerResultPanelRenderTargets(
    FrameworkElement ResultPanel,
    FrameworkElement CodeHintPanel,
    TextBlock FinalCode,
    TextBlock FinalLabel,
    TextBlock Warn);

public static class VsaCodeExplorerResultPanelRenderer
{
    public static void Apply(
        VsaCodeExplorerResultPanelPresentation presentation,
        VsaCodeExplorerResultPanelRenderTargets targets)
    {
        targets.ResultPanel.Visibility = presentation.ShowResultPanel ? Visibility.Visible : Visibility.Collapsed;
        targets.CodeHintPanel.Visibility = presentation.ShowCodeHintPanel ? Visibility.Visible : Visibility.Collapsed;

        if (!presentation.ShouldUpdateDetailPanels)
            return;

        targets.FinalCode.Text = presentation.FinalCodeText;
        targets.FinalLabel.Text = presentation.FinalLabelText;
        targets.Warn.Text = presentation.WarnText;
        targets.Warn.Visibility = presentation.ShowWarn ? Visibility.Visible : Visibility.Collapsed;
    }
}
