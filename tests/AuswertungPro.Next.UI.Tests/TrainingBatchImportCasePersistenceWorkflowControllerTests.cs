using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportCasePersistenceWorkflowControllerTests
{
    [Fact]
    public async Task PersistAsync_speichert_samples_aktualisiert_ui_und_speichert_state_wenn_faellig()
    {
        var existingSamples = new List<TrainingSample> { Sample("old", "AAA") };
        var newSamples = new List<TrainingSample>
        {
            Sample("new-1", "BBB"),
            Sample("new-2", "AAA")
        };
        var calls = new List<string>();
        List<TrainingSample>? savedSamples = null;

        await TrainingBatchImportCasePersistenceWorkflowController.PersistAsync(
            newSamples,
            existingSamples,
            processedCount: 5,
            saveSamplesAsync: samples =>
            {
                calls.Add($"save-samples:existing={existingSamples.Count}");
                savedSamples = samples;
                return Task.CompletedTask;
            },
            saveStateAsync: () =>
            {
                calls.Add("save-state");
                return Task.CompletedTask;
            },
            invokeOnUi: action =>
            {
                calls.Add("on-ui");
                action();
            },
            setSampleCount: value => calls.Add($"samples:{value}"),
            setCodesCovered: value => calls.Add($"codes:{value}"),
            log: message => calls.Add($"log:{message}"));

        Assert.Same(newSamples, savedSamples);
        Assert.Equal(new[] { "old", "new-1", "new-2" }, existingSamples.Select(s => s.SampleId));
        Assert.Equal(
            new[]
            {
                "save-samples:existing=1",
                "log:2 Samples als Kandidaten gespeichert (Status: Neu). Freigabe ueber Review (Modul I) - KEIN Auto-Index.",
                "on-ui",
                "samples:3",
                "codes:2",
                "log:  Gespeichert | Gesamt: 3 Samples, 2 Codes",
                "save-state"
            },
            calls);
    }

    [Fact]
    public async Task PersistAsync_speichert_state_nicht_wenn_intervall_nicht_faellig_ist()
    {
        var calls = new List<string>();

        await TrainingBatchImportCasePersistenceWorkflowController.PersistAsync(
            new List<TrainingSample> { Sample("new", "BBB") },
            new List<TrainingSample>(),
            processedCount: 4,
            saveSamplesAsync: _ => Task.CompletedTask,
            saveStateAsync: () =>
            {
                calls.Add("save-state");
                return Task.CompletedTask;
            },
            invokeOnUi: action => action(),
            setSampleCount: _ => { },
            setCodesCovered: _ => { },
            log: _ => { });

        Assert.DoesNotContain("save-state", calls);
    }

    [Fact]
    public async Task PersistAsync_schluckt_best_effort_state_save_fehler()
    {
        await TrainingBatchImportCasePersistenceWorkflowController.PersistAsync(
            new List<TrainingSample> { Sample("new", "BBB") },
            new List<TrainingSample>(),
            processedCount: 5,
            saveSamplesAsync: _ => Task.CompletedTask,
            saveStateAsync: () => throw new InvalidOperationException("kaputt"),
            invokeOnUi: action => action(),
            setSampleCount: _ => { },
            setCodesCovered: _ => { },
            log: _ => { });
    }

    private static TrainingSample Sample(string id, string code)
        => new()
        {
            SampleId = id,
            CaseId = "H-001",
            Code = code
        };
}
