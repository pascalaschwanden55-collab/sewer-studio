using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunWorkflowTests
{
    [Fact]
    public async Task RunAsync_baut_core_request_und_verdrahtet_ui_sinks()
    {
        var calls = new List<string>();
        TrainingBatchImportWorkflowRequest? captured = null;
        var token = new CancellationTokenSource().Token;
        var roots = new[] { @"D:\Training" };
        var cases = new List<TrainingCase>();

        await TrainingBatchImportRunWorkflow.RunAsync(
            new TrainingBatchImportRunWorkflowRequest(
                RootFolders: roots,
                DirectoryExists: folder => folder == @"D:\Training",
                ScanFolderAsync: folder =>
                {
                    calls.Add($"scan:{folder}");
                    return Task.FromResult<IReadOnlyList<TrainingCase>>([new TrainingCase { CaseId = "H1" }]);
                },
                Cases: cases,
                CodeCatalog: null,
                LoadRuntimeSettings: () => RuntimeSettings(),
                LoadSettingsAsync: () => Task.FromResult(new TrainingCenterSettings()),
                LoadSamplesAsync: () => Task.FromResult(new List<TrainingSample>()),
                MergeAndSaveSamplesAsync: samples =>
                {
                    calls.Add($"merge:{samples.Count}");
                    return Task.CompletedTask;
                },
                SaveStateAsync: () =>
                {
                    calls.Add("save-state");
                    return Task.CompletedTask;
                },
                ExtractPreviewFrameAsync: (_, _, _) => Task.FromResult<string?>("frame.jpg"),
                GetSelfTrainingResultCount: () => 7,
                SetBusy: value => calls.Add($"busy:{value}"),
                SetLogText: value => calls.Add($"log-text:{value}"),
                SetProgressValue: value => calls.Add($"progress-value:{value}"),
                SetProgressMax: value => calls.Add($"progress-max:{value}"),
                SetStatusText: value => calls.Add($"status:{value}"),
                Log: value => calls.Add($"log:{value}"),
                UpdateLivePreview: preview => calls.Add($"preview:{preview.CaseInfo}:{preview.CodeInfo}:{preview.MeterInfo}:{preview.FramePath}"),
                OnUi: action =>
                {
                    calls.Add("on-ui");
                    action();
                },
                AddResult: result => calls.Add($"result:{result.VsaCode}"),
                UpdateCodeDistribution: (code, level) => calls.Add($"distribution:{code}:{level}"),
                SetKbSampleCount: value => calls.Add($"kb-samples:{value}"),
                SetKbCodesCovered: value => calls.Add($"kb-codes:{value}"),
                ReplaceSamples: samples => calls.Add($"replace-samples:{samples.Count}"),
                RefreshKbStatusAsync: () =>
                {
                    calls.Add("refresh-kb");
                    return Task.CompletedTask;
                },
                ClearLivePreview: () => calls.Add("clear-preview"),
                ResetSelfTrainingVisuals: () => calls.Add("reset-visuals"),
                BeginActivity: () =>
                {
                    calls.Add("activity-start");
                    return new TrackingDisposable(calls);
                },
                RunWorkflowAsync: request =>
                {
                    captured = request;
                    calls.Add("run-core");
                    return Task.CompletedTask;
                }),
            token);

        Assert.NotNull(captured);
        Assert.Same(roots, captured!.RootFolders);
        Assert.Same(cases, captured.Cases);
        Assert.Equal(token, captured.CancellationToken);
        Assert.True(captured.DirectoryExists(@"D:\Training"));
        Assert.Equal(7, captured.GetSelfTrainingResultCount());

        captured.BatchUi.SetBusy(true);
        captured.BatchUi.SetLogText("");
        captured.BatchUi.SetProgressValue(2);
        captured.BatchUi.SetProgressMax(9);
        captured.BatchUi.SetStatusText("läuft");
        captured.BatchUi.Log("meldung");
        captured.CaseUi.UpdateLivePreview(new TrainingBatchImportLivePreview("case", "code", "meter", "frame"));
        captured.CaseUi.InvokeOnUi(() => calls.Add("inside-ui"));
        captured.CaseUi.AddResult(new SelfTrainingEntryResult { VsaCode = "BBA" });
        captured.CaseUi.UpdateCodeDistribution("BBA", MatchLevel.ExactMatch);
        captured.CaseUi.SetSampleCount(3);
        captured.CaseUi.SetCodesCovered(2);
        captured.CaseUi.Log("case-log");
        captured.ReplaceSamples([new TrainingSample()]);
        await captured.RefreshKbStatusAsync();
        captured.ClearLivePreview();
        captured.ResetSelfTrainingVisuals();
        using (captured.BeginActivity())
        {
        }

        Assert.Contains("run-core", calls);
        Assert.Contains("busy:True", calls);
        Assert.Contains("progress-value:2", calls);
        Assert.Contains("status:läuft", calls);
        Assert.Contains("preview:case:code:meter:frame", calls);
        Assert.Contains("on-ui", calls);
        Assert.Contains("inside-ui", calls);
        Assert.Contains("result:BBA", calls);
        Assert.Contains("distribution:BBA:ExactMatch", calls);
        Assert.Contains("kb-samples:3", calls);
        Assert.Contains("kb-codes:2", calls);
        Assert.Contains("replace-samples:1", calls);
        Assert.Contains("refresh-kb", calls);
        Assert.Contains("clear-preview", calls);
        Assert.Contains("reset-visuals", calls);
        Assert.Contains("activity-start", calls);
        Assert.Contains("activity-dispose", calls);
    }

    private static AiRuntimeSettings RuntimeSettings()
        => new(
            Enabled: false,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision",
            TextModel: "text",
            EmbedModel: "embed",
            FfmpegPath: "ffmpeg",
            OllamaRequestTimeout: TimeSpan.FromMinutes(2),
            OllamaKeepAlive: "5m",
            OllamaNumCtx: 2048);

    private sealed class TrackingDisposable(List<string> calls) : IDisposable
    {
        public void Dispose() => calls.Add("activity-dispose");
    }
}
