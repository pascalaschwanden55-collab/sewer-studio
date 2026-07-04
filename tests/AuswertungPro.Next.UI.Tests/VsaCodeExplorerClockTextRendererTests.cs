using System;
using System.Threading;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerClockTextRendererTests
{
    [Fact]
    public void ApplyVonChanged_setzt_bis_wenn_workflow_einen_wert_liefert()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(clockBis: "alt", transfer: "alt-transfer");

            VsaCodeExplorerClockTextRenderer.ApplyVonChanged(
                new VsaCodeExplorerClockVonChangedResult(
                    ClockVon: "6",
                    ClockBisText: "00",
                    TransferText: "Transfer: 06 00"),
                targets);

            Assert.Equal("00", targets.ClockBisTextBox.Text);
            Assert.Equal("Transfer: 06 00", targets.ClockTransferTextBlock.Text);
        });
    }

    [Fact]
    public void ApplyVonChanged_laesst_bis_unveraendert_wenn_workflow_keinen_wert_liefert()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(clockBis: "09", transfer: "alt-transfer");

            VsaCodeExplorerClockTextRenderer.ApplyVonChanged(
                new VsaCodeExplorerClockVonChangedResult(
                    ClockVon: "6",
                    ClockBisText: null,
                    TransferText: "Transfer: 06 09"),
                targets);

            Assert.Equal("09", targets.ClockBisTextBox.Text);
            Assert.Equal("Transfer: 06 09", targets.ClockTransferTextBlock.Text);
        });
    }

    [Fact]
    public void ApplyBisChanged_setzt_transfertext()
    {
        RunSta(() =>
        {
            var targets = CreateTargets(clockBis: "09", transfer: "alt-transfer");

            VsaCodeExplorerClockTextRenderer.ApplyBisChanged(
                new VsaCodeExplorerClockBisChangedResult(
                    ClockBis: "09",
                    TransferText: "Transfer: 06 09"),
                targets);

            Assert.Equal("09", targets.ClockBisTextBox.Text);
            Assert.Equal("Transfer: 06 09", targets.ClockTransferTextBlock.Text);
        });
    }

    private static VsaCodeExplorerClockTextRenderTargets CreateTargets(string clockBis, string transfer)
        => new(
            new TextBox { Text = clockBis },
            new TextBlock { Text = transfer });

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
