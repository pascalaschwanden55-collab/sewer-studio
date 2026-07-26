using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public sealed record VsaCodeExplorerInitialFieldValues(
    string MeterStart,
    string MeterEnd,
    string Bemerkungen,
    string Q1Value,
    string Q2Value,
    string ClockVon,
    string ClockBis);

public sealed record VsaCodeExplorerInitialFieldsRenderTargets(
    TextBox MeterStartTextBox,
    TextBox MeterEndTextBox,
    TextBox BemerkungenTextBox,
    TextBox Q1ValueTextBox,
    TextBox Q2ValueTextBox,
    TextBox ClockVonTextBox,
    TextBox ClockBisTextBox);

public static class VsaCodeExplorerInitialFieldsRenderer
{
    public static void Apply(
        VsaCodeExplorerInitialFieldValues values,
        VsaCodeExplorerInitialFieldsRenderTargets targets)
    {
        targets.MeterStartTextBox.Text = values.MeterStart;
        targets.MeterEndTextBox.Text = values.MeterEnd;
        targets.BemerkungenTextBox.Text = values.Bemerkungen;
        targets.Q1ValueTextBox.Text = values.Q1Value;
        targets.Q2ValueTextBox.Text = values.Q2Value;
        targets.ClockVonTextBox.Text = values.ClockVon;
        targets.ClockBisTextBox.Text = values.ClockBis;
    }
}
