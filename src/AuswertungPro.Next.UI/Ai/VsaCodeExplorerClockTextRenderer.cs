using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerClockTextRenderTargets(
    TextBox ClockBisTextBox,
    TextBlock ClockTransferTextBlock);

public static class VsaCodeExplorerClockTextRenderer
{
    public static void ApplyVonChanged(
        VsaCodeExplorerClockVonChangedResult result,
        VsaCodeExplorerClockTextRenderTargets targets)
    {
        if (result.ClockBisText is not null)
            targets.ClockBisTextBox.Text = result.ClockBisText;

        targets.ClockTransferTextBlock.Text = result.TransferText;
    }

    public static void ApplyBisChanged(
        VsaCodeExplorerClockBisChangedResult result,
        VsaCodeExplorerClockTextRenderTargets targets)
    {
        targets.ClockTransferTextBlock.Text = result.TransferText;
    }
}
