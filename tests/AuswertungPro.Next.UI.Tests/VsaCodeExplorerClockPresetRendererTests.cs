using System;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerClockPresetRendererTests
{
    [Fact]
    public void Apply_setzt_clock_textboxen_wenn_preset_gueltig_ist()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(von: "alt-von", bis: "alt-bis");

            VsaCodeExplorerClockPresetRenderer.Apply(
                new VsaCodeExplorerClockPresetResult(
                    ShouldApply: true,
                    ClockVonText: "03",
                    ClockBisText: "09"),
                targets);

            Assert.Equal("03", targets.ClockVonTextBox.Text);
            Assert.Equal("09", targets.ClockBisTextBox.Text);
        });
    }

    [Fact]
    public void Apply_laesst_textboxen_unveraendert_wenn_preset_ungueltig_ist()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(von: "alt-von", bis: "alt-bis");

            VsaCodeExplorerClockPresetRenderer.Apply(
                new VsaCodeExplorerClockPresetResult(
                    ShouldApply: false,
                    ClockVonText: "",
                    ClockBisText: ""),
                targets);

            Assert.Equal("alt-von", targets.ClockVonTextBox.Text);
            Assert.Equal("alt-bis", targets.ClockBisTextBox.Text);
        });
    }

    private static VsaCodeExplorerClockPresetRenderTargets CreateTargets(string von, string bis)
        => new(
            new TextBox { Text = von },
            new TextBox { Text = bis });

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw failure;
    }
}
