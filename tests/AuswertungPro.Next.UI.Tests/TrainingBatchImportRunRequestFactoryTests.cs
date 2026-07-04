using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportRunRequestFactoryTests
{
    [Fact]
    public async Task CreateWithDefaults_mappt_import_inputs_in_factory()
    {
        var inspectionDate = new DateTime(2026, 2, 3);
        var input = new TrainingCaseInput(
            CaseId: "batch-case",
            FolderPath: "batch-folder",
            VideoPath: "batch-video.mp4",
            ProtocolPath: "batch-protocol.pdf",
            InspectionDate: inspectionDate);

        var request = TrainingBatchImportRunRequestFactory.CreateWithDefaults(
            new TrainingBatchImportRunDefaultRequestFactoryRequest(
                RootFolders: [@"D:\Training"],
                ScanInputsAsync: _ => Task.FromResult(new List<TrainingCaseInput> { input }),
                Cases: new List<TrainingCase>(),
                CodeCatalog: null,
                SaveStateAsync: () => Task.CompletedTask,
                GetSelfTrainingResultCount: () => 0,
                SetBusy: _ => { },
                SetLogText: _ => { },
                SetProgressValue: _ => { },
                SetProgressMax: _ => { },
                SetStatusText: _ => { },
                Log: _ => { },
                UpdateLivePreview: _ => { },
                OnUi: action => action(),
                AddResult: _ => { },
                UpdateCodeDistribution: (_, _) => { },
                SetKbSampleCount: _ => { },
                SetKbCodesCovered: _ => { },
                Samples: [],
                RefreshKbStatusAsync: () => Task.CompletedTask,
                ClearLivePreview: () => { },
                ResetSelfTrainingVisuals: () => { }));

        var cases = await request.ScanFolderAsync(@"D:\Training");

        var item = Assert.Single(cases);
        Assert.Equal("batch-case", item.CaseId);
        Assert.Equal("batch-folder", item.FolderPath);
        Assert.Equal("batch-video.mp4", item.VideoPath);
        Assert.Equal("batch-protocol.pdf", item.ProtocolPath);
        Assert.Equal(inspectionDate, item.InspectionDate);
        Assert.Equal(TrainingCaseStatus.New, item.Status);
        Assert.NotNull(request.ExtractPreviewFrameAsync);
    }

    [Fact]
    public async Task Create_verdrahtet_viewmodel_delegates_und_defaults()
    {
        var calls = new List<string>();
        var roots = new[] { @"D:\Training" };
        var cases = new List<TrainingCase>();
        var samples = new ObservableCollection<TrainingSample> { new() { SampleId = "old" } };
        var runtimeSettings = RuntimeSettings();
        var centerSettings = new TrainingCenterSettings { GpuConcurrency = 3 };
        var loadedSamples = new List<TrainingSample> { new() { SampleId = "loaded" } };
        TrainingBatchImportWorkflowRequest? capturedWorkflowRequest = null;

        var request = TrainingBatchImportRunRequestFactory.Create(
            new TrainingBatchImportRunRequestFactoryRequest(
                RootFolders: roots,
                ScanFolderAsync: folder =>
                {
                    calls.Add($"scan:{folder}");
                    return Task.FromResult<IReadOnlyList<TrainingCase>>([new TrainingCase { CaseId = "scan-case" }]);
                },
                Cases: cases,
                CodeCatalog: null,
                SaveStateAsync: () =>
                {
                    calls.Add("save-state");
                    return Task.CompletedTask;
                },
                ExtractPreviewFrameAsync: (_, _, _) =>
                {
                    calls.Add("extract-preview");
                    return Task.FromResult<string?>("frame.jpg");
                },
                GetSelfTrainingResultCount: () => 42,
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
                Samples: samples,
                RefreshKbStatusAsync: () =>
                {
                    calls.Add("refresh-kb");
                    return Task.CompletedTask;
                },
                ClearLivePreview: () => calls.Add("clear-preview"),
                ResetSelfTrainingVisuals: () => calls.Add("reset-visuals")),
            new TrainingBatchImportRunRequestFactoryDefaults(
                DirectoryExists: folder => folder == @"D:\Training",
                LoadRuntimeSettings: () =>
                {
                    calls.Add("load-runtime");
                    return runtimeSettings;
                },
                LoadSettingsAsync: () =>
                {
                    calls.Add("load-settings");
                    return Task.FromResult(centerSettings);
                },
                LoadSamplesAsync: () =>
                {
                    calls.Add("load-samples");
                    return Task.FromResult(loadedSamples);
                },
                MergeAndSaveSamplesAsync: mergedSamples =>
                {
                    calls.Add($"merge-save:{mergedSamples.Count}");
                    return Task.CompletedTask;
                },
                RunWorkflowAsync: workflowRequest =>
                {
                    capturedWorkflowRequest = workflowRequest;
                    calls.Add("run-core");
                    return Task.CompletedTask;
                },
                BeginActivity: () =>
                {
                    calls.Add("activity-start");
                    return new TrackingDisposable(calls);
                }));

        Assert.Same(roots, request.RootFolders);
        Assert.Same(cases, request.Cases);
        Assert.True(request.DirectoryExists(@"D:\Training"));
        Assert.Equal(42, request.GetSelfTrainingResultCount());
        Assert.Same(runtimeSettings, request.LoadRuntimeSettings());
        Assert.Same(centerSettings, await request.LoadSettingsAsync());
        Assert.Same(loadedSamples, await request.LoadSamplesAsync());

        var scanned = await request.ScanFolderAsync("input");
        Assert.Equal("scan-case", scanned.Single().CaseId);
        await request.MergeAndSaveSamplesAsync([new TrainingSample(), new TrainingSample()]);
        await request.SaveStateAsync();
        Assert.Equal("frame.jpg", await request.ExtractPreviewFrameAsync(new TrainingCase(), runtimeSettings, CancellationToken.None));

        request.SetBusy(true);
        request.SetLogText("log-text");
        request.SetProgressValue(2);
        request.SetProgressMax(9);
        request.SetStatusText("laeuft");
        request.Log("meldung");
        request.UpdateLivePreview(new TrainingBatchImportLivePreview("case", "code", "meter", "frame"));
        request.OnUi(() => calls.Add("inside-ui"));
        request.AddResult(new SelfTrainingEntryResult { VsaCode = "BBA" });
        request.UpdateCodeDistribution("BBA", MatchLevel.ExactMatch);
        request.SetKbSampleCount(3);
        request.SetKbCodesCovered(2);
        request.ReplaceSamples([new TrainingSample { SampleId = "new" }]);
        await request.RefreshKbStatusAsync();
        request.ClearLivePreview();
        request.ResetSelfTrainingVisuals();
        using (request.BeginActivity())
        {
        }
        await request.RunWorkflowAsync(null!);

        Assert.Single(samples);
        Assert.Equal("new", samples[0].SampleId);
        Assert.Null(capturedWorkflowRequest);
        Assert.Contains("load-runtime", calls);
        Assert.Contains("load-settings", calls);
        Assert.Contains("load-samples", calls);
        Assert.Contains("scan:input", calls);
        Assert.Contains("merge-save:2", calls);
        Assert.Contains("save-state", calls);
        Assert.Contains("extract-preview", calls);
        Assert.Contains("busy:True", calls);
        Assert.Contains("status:laeuft", calls);
        Assert.Contains("preview:case:code:meter:frame", calls);
        Assert.Contains("on-ui", calls);
        Assert.Contains("inside-ui", calls);
        Assert.Contains("result:BBA", calls);
        Assert.Contains("distribution:BBA:ExactMatch", calls);
        Assert.Contains("kb-samples:3", calls);
        Assert.Contains("kb-codes:2", calls);
        Assert.Contains("refresh-kb", calls);
        Assert.Contains("clear-preview", calls);
        Assert.Contains("reset-visuals", calls);
        Assert.Contains("activity-start", calls);
        Assert.Contains("activity-dispose", calls);
        Assert.Contains("run-core", calls);
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
