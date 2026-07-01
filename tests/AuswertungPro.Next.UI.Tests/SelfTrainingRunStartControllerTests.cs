using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunStartControllerTests
{
    [Fact]
    public void Apply_setzt_startzustand_und_startanzeige()
    {
        var trainingCase = new TrainingCase
        {
            CaseId = "101.1-102.1",
            ProtocolPath = @"C:\Import\protokoll.pdf"
        };
        var calls = new List<string>();
        var ui = new SelfTrainingUiSink(
            setBusy: value => calls.Add($"busy:{value}"),
            setSelfTrainingRunning: value => calls.Add($"running:{value}"),
            setLogText: value => calls.Add($"logtext:{value}"),
            setStatusText: value => calls.Add($"status:{value}"),
            log: value => calls.Add($"log:{value}"));

        SelfTrainingRunStartController.Apply(
            trainingCase,
            ui,
            () => calls.Add("reset"));

        Assert.Equal(
            new[]
            {
                "busy:True",
                "running:True",
                "reset",
                "logtext:",
                "status:Selbsttraining: 101.1-102.1...",
                "log:--- Selbsttraining starten: 101.1-102.1 ---",
                @"log:  Protokoll: C:\Import\protokoll.pdf"
            },
            calls);
    }
}
