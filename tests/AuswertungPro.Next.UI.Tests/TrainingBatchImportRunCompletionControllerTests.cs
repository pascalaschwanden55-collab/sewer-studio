using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunCompletionControllerTests
{
    [Fact]
    public async Task CompleteAsync_laedt_samples_und_stoppt_bei_no_new_status_ohne_refresh_und_save()
    {
        var summary = new TrainingBatchImportRunSummary();
        var samples = new List<TrainingSample> { Sample("old") };
        var logLines = new List<string>();
        var statusText = "";
        var refreshCalls = 0;
        var saveCalls = 0;

        var result = await TrainingBatchImportRunCompletionController.CompleteAsync(
            summary,
            processedCaseCount: 2,
            loadSamplesAsync: () => Task.FromResult<IReadOnlyList<TrainingSample>>(
                new List<TrainingSample> { Sample("new") }),
            clearSamples: samples.Clear,
            addSample: samples.Add,
            refreshKbStatusAsync: () =>
            {
                refreshCalls++;
                return Task.CompletedTask;
            },
            saveStateAsync: () =>
            {
                saveCalls++;
                return Task.CompletedTask;
            },
            log: logLines.Add,
            setStatus: value => statusText = value);

        Assert.True(result.ShouldStop);
        Assert.Single(samples);
        Assert.Equal("new", samples[0].SampleId);
        Assert.Equal("0 neue Samples aus 2 Faellen.", statusText);
        Assert.Single(logLines, statusText);
        Assert.Equal(0, refreshCalls);
        Assert.Equal(0, saveCalls);
    }

    [Fact]
    public async Task CompleteAsync_loggt_finalstatus_refresht_kb_speichert_state_und_loggt_abschluss()
    {
        var summary = new TrainingBatchImportRunSummary();
        summary.AddNewSamples(3);
        var samples = new List<TrainingSample>();
        var calls = new List<string>();
        var statusText = "";

        var result = await TrainingBatchImportRunCompletionController.CompleteAsync(
            summary,
            processedCaseCount: 2,
            loadSamplesAsync: () => Task.FromResult<IReadOnlyList<TrainingSample>>(
                new List<TrainingSample> { Sample("s1"), Sample("s2") }),
            clearSamples: () =>
            {
                calls.Add("clear");
                samples.Clear();
            },
            addSample: sample =>
            {
                calls.Add($"add:{sample.SampleId}");
                samples.Add(sample);
            },
            refreshKbStatusAsync: () =>
            {
                calls.Add("refresh-kb");
                return Task.CompletedTask;
            },
            saveStateAsync: () =>
            {
                calls.Add("save-state");
                return Task.CompletedTask;
            },
            log: line => calls.Add($"log:{line}"),
            setStatus: value =>
            {
                calls.Add($"status:{value}");
                statusText = value;
            });

        var finalStatus = summary.BuildCompletionStatus();
        Assert.False(result.ShouldStop);
        Assert.Equal(new[] { "s1", "s2" }, samples.Select(s => s.SampleId));
        Assert.Equal(finalStatus, statusText);
        Assert.Contains($"log:{finalStatus}", calls);
        Assert.Contains("refresh-kb", calls);
        Assert.Contains("save-state", calls);
        Assert.Equal("log:F\u00e4lle gespeichert. Batch-Import abgeschlossen.", calls[^1]);
    }

    private static TrainingSample Sample(string id)
        => new()
        {
            SampleId = id,
            CaseId = "H-001",
            Code = "BAB"
        };
}
