using System;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerInitialFieldsRendererTests
{
    [Fact]
    public void Apply_setzt_alle_initialen_textfelder()
    {
        RunSta(() =>
        {
            var targets = CreateTargets();

            VsaCodeExplorerInitialFieldsRenderer.Apply(
                new VsaCodeExplorerInitialFieldValues(
                    MeterStart: "1.20",
                    MeterEnd: "9.80",
                    Bemerkungen: "Kontrolle",
                    Q1Value: "12",
                    Q2Value: "34",
                    ClockVon: "03",
                    ClockBis: "09"),
                targets);

            Assert.Equal("1.20", targets.MeterStartTextBox.Text);
            Assert.Equal("9.80", targets.MeterEndTextBox.Text);
            Assert.Equal("Kontrolle", targets.BemerkungenTextBox.Text);
            Assert.Equal("12", targets.Q1ValueTextBox.Text);
            Assert.Equal("34", targets.Q2ValueTextBox.Text);
            Assert.Equal("03", targets.ClockVonTextBox.Text);
            Assert.Equal("09", targets.ClockBisTextBox.Text);
        });
    }

    private static VsaCodeExplorerInitialFieldsRenderTargets CreateTargets()
        => new(
            new TextBox(),
            new TextBox(),
            new TextBox(),
            new TextBox(),
            new TextBox(),
            new TextBox(),
            new TextBox());

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
