using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunFinalizerControllerTests
{
    [Fact]
    public void Apply_setzt_busy_running_und_orchestrator_zurueck()
    {
        var calls = new List<string>();

        SelfTrainingRunFinalizerController.Apply(
            value => calls.Add($"busy:{value}"),
            value => calls.Add($"running:{value}"),
            () => calls.Add("orchestrator:null"));

        Assert.Equal(
            new[]
            {
                "busy:False",
                "running:False",
                "orchestrator:null"
            },
            calls);
    }
}
