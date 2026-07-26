using System;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerClockPickerRendererTests
{
    [Fact]
    public void ApplySingleValueChanged_setzt_von_und_bis_bei_gueltigem_wert()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(von: "alt-von", bis: "alt-bis");

            VsaCodeExplorerClockPickerRenderer.ApplySingleValueChanged("03", targets);

            Assert.Equal("03", targets.ClockVonTextBox.Text);
            Assert.Equal("00", targets.ClockBisTextBox.Text);
        });
    }

    [Fact]
    public void ApplySingleValueChanged_laesst_textboxen_bei_leerem_wert_unveraendert()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(von: "alt-von", bis: "alt-bis");

            VsaCodeExplorerClockPickerRenderer.ApplySingleValueChanged(" ", targets);

            Assert.Equal("alt-von", targets.ClockVonTextBox.Text);
            Assert.Equal("alt-bis", targets.ClockBisTextBox.Text);
        });
    }

    [Fact]
    public void ApplyRangeFromChanged_setzt_von_und_normalisiert_null_zu_leer()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(von: "alt-von", bis: "alt-bis");

            VsaCodeExplorerClockPickerRenderer.ApplyRangeFromChanged(null, targets);

            Assert.Equal("", targets.ClockVonTextBox.Text);
            Assert.Equal("alt-bis", targets.ClockBisTextBox.Text);
        });
    }

    [Fact]
    public void ApplyRangeToChanged_setzt_bis_und_normalisiert_null_zu_leer()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(von: "alt-von", bis: "alt-bis");

            VsaCodeExplorerClockPickerRenderer.ApplyRangeToChanged(null, targets);

            Assert.Equal("alt-von", targets.ClockVonTextBox.Text);
            Assert.Equal("", targets.ClockBisTextBox.Text);
        });
    }

    private static VsaCodeExplorerClockPickerRenderTargets CreateTargets(string von, string bis)
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
