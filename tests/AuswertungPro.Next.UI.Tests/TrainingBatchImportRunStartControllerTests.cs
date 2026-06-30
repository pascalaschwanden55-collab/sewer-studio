using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunStartControllerTests
{
    [Fact]
    public void Apply_setzt_startzustand_in_bisheriger_reihenfolge()
    {
        var calls = new List<string>();
        var ui = new TrainingBatchUiSink(
            SetBusy: value => calls.Add($"busy:{value}"),
            SetLogText: value => calls.Add($"log:{value}"),
            SetProgressValue: value => calls.Add($"progress:{value}"),
            SetProgressMax: value => calls.Add($"max:{value}"));

        TrainingBatchImportRunStartController.Apply(
            ui,
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
