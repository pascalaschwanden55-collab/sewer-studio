using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunStartControllerTests
{
    [Fact]
    public void Apply_setzt_startzustand_in_bisheriger_reihenfolge()
    {
        var calls = new List<string>();

        TrainingBatchImportRunStartController.Apply(
            value => calls.Add($"busy:{value}"),
            value => calls.Add($"log:{value}"),
            value => calls.Add($"progress:{value}"),
            value => calls.Add($"max:{value}"),
            () => calls.Add("clear-preview"),
            () => calls.Add("reset-visuals"));

        Assert.Equal(
            new[]
            {
                "busy:True",
                "log:",
                "progress:0",
                "max:1",
                "clear-preview",
                "reset-visuals"
            },
            calls);
    }
}
