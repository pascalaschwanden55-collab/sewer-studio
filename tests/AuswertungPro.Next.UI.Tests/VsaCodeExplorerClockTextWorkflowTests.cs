using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Vsa;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerClockTextWorkflowTests
{
    [Fact]
    public void ApplyVonChanged_setzt_bis_auf_00_im_single_modus()
    {
        var result = VsaCodeExplorerClockTextWorkflow.ApplyVonChanged(
            clockVonText: "6",
            currentClockBisText: "",
            clockMode: "single");

        Assert.Equal("6", result.ClockVon);
        Assert.Equal("00", result.ClockBisText);
        Assert.Equal("Transfer: 06 00", result.TransferText);
    }

    [Fact]
    public void ApplyVonChanged_leert_bis_im_single_modus_wenn_von_leer_ist()
    {
        var result = VsaCodeExplorerClockTextWorkflow.ApplyVonChanged(
            clockVonText: " ",
            currentClockBisText: "00",
            clockMode: "single");

        Assert.Equal(" ", result.ClockVon);
        Assert.Equal("", result.ClockBisText);
        Assert.Equal("Transfer: -- --", result.TransferText);
    }

    [Fact]
    public void ApplyVonChanged_laesst_bis_im_range_modus_unveraendert()
    {
        var result = VsaCodeExplorerClockTextWorkflow.ApplyVonChanged(
            clockVonText: "6",
            currentClockBisText: "9",
            clockMode: "range");

        Assert.Equal("6", result.ClockVon);
        Assert.Null(result.ClockBisText);
        Assert.Equal("Transfer: 06 09", result.TransferText);
    }

    [Fact]
    public void ApplyBisChanged_aktualisiert_bis_und_transfer()
    {
        var result = VsaCodeExplorerClockTextWorkflow.ApplyBisChanged(
            clockVonText: "6",
            clockBisText: "9");

        Assert.Equal("9", result.ClockBis);
        Assert.Equal("Transfer: 06 09", result.TransferText);
    }
}
