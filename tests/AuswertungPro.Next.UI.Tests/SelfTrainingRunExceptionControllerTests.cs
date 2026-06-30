using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunExceptionControllerTests
{
    [Fact]
    public void ApplyCanceled_loggt_und_setzt_abbruchstatus()
    {
        var logLines = new List<string>();
        var statusText = "";

        SelfTrainingRunExceptionController.ApplyCanceled(
            logLines.Add,
            value => statusText = value);

        Assert.Single(logLines, "Selbsttraining abgebrochen.");
        Assert.Equal("Selbsttraining abgebrochen.", statusText);
    }

    [Fact]
    public void ApplyFailure_loggt_typ_und_message_und_setzt_fehlerstatus()
    {
        var logLines = new List<string>();
        var statusText = "";

        SelfTrainingRunExceptionController.ApplyFailure(
            new InvalidOperationException("Kaputt"),
            logLines.Add,
            value => statusText = value);

        Assert.Single(logLines, "FEHLER: InvalidOperationException: Kaputt");
        Assert.Equal("Fehler: Kaputt", statusText);
    }
}
