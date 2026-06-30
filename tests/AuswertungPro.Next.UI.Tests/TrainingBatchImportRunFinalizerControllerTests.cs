using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunFinalizerControllerTests
{
    [Fact]
    public void Apply_setzt_busy_zurueck()
    {
        var calls = new List<string>();

        TrainingBatchImportRunFinalizerController.Apply(
            value => calls.Add($"busy:{value}"));

        Assert.Equal(
            new[]
            {
                "busy:False"
            },
            calls);
    }
}
