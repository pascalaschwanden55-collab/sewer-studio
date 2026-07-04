using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerClockPresetRenderTargets(
    TextBox ClockVonTextBox,
    TextBox ClockBisTextBox);

public static class VsaCodeExplorerClockPresetRenderer
{
    public static void Apply(
        VsaCodeExplorerClockPresetResult result,
        VsaCodeExplorerClockPresetRenderTargets targets)
    {
        if (!result.ShouldApply)
            return;

        targets.ClockVonTextBox.Text = result.ClockVonText;
        targets.ClockBisTextBox.Text = result.ClockBisText;
    }
}
