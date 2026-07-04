using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerPhotoResultRenderTargets(
    TextBox Q1ValueTextBox,
    TextBox ClockVonTextBox);

public static class VsaCodeExplorerPhotoResultRenderer
{
    public static void Apply(
        VsaCodeExplorerPhotoResultApplyResult result,
        VsaCodeExplorerPhotoResultRenderTargets targets)
    {
        if (result.Q1Value is not null)
            targets.Q1ValueTextBox.Text = result.Q1Value;

        if (result.ClockVon is not null)
            targets.ClockVonTextBox.Text = result.ClockVon;
    }
}
