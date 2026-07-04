using System;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerPhotoResultRendererTests
{
    [Fact]
    public void Apply_setzt_q1_und_uhr_von_wenn_werte_vorhanden_sind()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(q1: "alt-q1", clockVon: "03");

            VsaCodeExplorerPhotoResultRenderer.Apply(
                new VsaCodeExplorerPhotoResultApplyResult(
                    Q1Value: "42",
                    ClockVon: "06",
                    PhotoPathChanged: false,
                    UpdatedCalibration: null),
                targets);

            Assert.Equal("42", targets.Q1ValueTextBox.Text);
            Assert.Equal("06", targets.ClockVonTextBox.Text);
        });
    }

    [Fact]
    public void Apply_laesst_textboxen_unveraendert_wenn_keine_werte_vorhanden_sind()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(q1: "alt-q1", clockVon: "03");

            VsaCodeExplorerPhotoResultRenderer.Apply(
                new VsaCodeExplorerPhotoResultApplyResult(
                    Q1Value: null,
                    ClockVon: null,
                    PhotoPathChanged: false,
                    UpdatedCalibration: null),
                targets);

            Assert.Equal("alt-q1", targets.Q1ValueTextBox.Text);
            Assert.Equal("03", targets.ClockVonTextBox.Text);
        });
    }

    private static VsaCodeExplorerPhotoResultRenderTargets CreateTargets(string q1, string clockVon)
        => new(
            new TextBox { Text = q1 },
            new TextBox { Text = clockVon });

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
