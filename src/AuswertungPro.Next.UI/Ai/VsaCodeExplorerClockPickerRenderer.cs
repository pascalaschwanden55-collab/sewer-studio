using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerClockPickerRenderTargets(
    TextBox ClockVonTextBox,
    TextBox ClockBisTextBox);

public static class VsaCodeExplorerClockPickerRenderer
{
    public static void ApplySingleValueChanged(
        string? value,
        VsaCodeExplorerClockPickerRenderTargets targets)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        targets.ClockVonTextBox.Text = value;
        targets.ClockBisTextBox.Text = "00";
    }

    public static void ApplyRangeFromChanged(
        string? value,
        VsaCodeExplorerClockPickerRenderTargets targets)
    {
        targets.ClockVonTextBox.Text = value ?? "";
    }

    public static void ApplyRangeToChanged(
        string? value,
        VsaCodeExplorerClockPickerRenderTargets targets)
    {
        targets.ClockBisTextBox.Text = value ?? "";
    }
}
