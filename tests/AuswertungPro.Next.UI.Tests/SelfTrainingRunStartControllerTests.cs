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

        SelfTrainingRunStartController.Apply(
            trainingCase,
            value => calls.Add($"busy:{value}"),
            value => calls.Add($"running:{value}"),
            () => calls.Add("reset"),
            value => calls.Add($"logtext:{value}"),
            value => calls.Add($"status:{value}"),
            value => calls.Add($"log:{value}"));

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
