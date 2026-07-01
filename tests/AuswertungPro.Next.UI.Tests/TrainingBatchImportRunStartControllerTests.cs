using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunStartControllerTests
{
    [Fact]
    public void Apply_setzt_startzustand_in_bisheriger_reihenfolge()
    {
        var calls = new List<string>();
        var ui = new TrainingBatchUiSink(
            setBusy: value => calls.Add($"busy:{value}"),
            setLogText: value => calls.Add($"log:{value}"),
            setProgressValue: value => calls.Add($"progress:{value}"),
            setProgressMax: value => calls.Add($"max:{value}"),
            setStatusText: _ => { },
            log: _ => { });

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
