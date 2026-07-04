using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunRequestFactoryTests
{
    [Fact]
    public async Task Create_verdrahtet_viewmodel_delegates_und_defaults()
    {
        var calls = new List<string>();
        var selectedCase = new TrainingCase { CaseId = "case-1" };
        var tokenSource = new CancellationTokenSource();
        var reviewQueue = new InfraSelfImproving.ReviewQueueService();
        var cachedHttpClient = new HttpClient();
        var setup = CreateSetup(calls);
        var loadedSamples = new List<TrainingSample>
        {
            new()
            {
                SampleId = "sample-1",
                CaseId = "case-1",
                Status = TrainingSampleStatus.Approved,
                KbIndexState = KbIndexState.None
            }
        };

        var request = SelfTrainingRunRequestFactory.Create(
            new SelfTrainingRunRequestFactoryRequest(
                SelectedCase: selectedCase,
                SetBusy: value => calls.Add($"busy:{value}"),
                SetSelfTrainingRunning: value => calls.Add($"running:{value}"),
                SetLogText: value => calls.Add($"log-text:{value}"),
                SetStatusText: value => calls.Add($"status:{value}"),
                Log: value => calls.Add($"log:{value}"),
                GetKbHttpClient: () =>
                {
                    calls.Add("get-http");
                    return cachedHttpClient;
                },
                SetKbHttpClient: _ => calls.Add("set-http"),
                AppSettings: null,
                CodeCatalog: null,
                SetActiveVisionModel: value => calls.Add($"active-model:{value}"),
                SetOrchestrator: value => calls.Add($"orchestrator:{value?.GetType().Name ?? "null"}"),
                OnProgress: _ => calls.Add("progress"),
                IndexSamplesAsync: (samples, token) =>
                {
                    calls.Add($"index:{samples.Count}:{token.CanBeCanceled}");
                    return Task.FromResult(new KbIndexOutcome([samples[0].SampleId], []));
                },
                ReviewQueueService: reviewQueue,
                ReloadReviewQueue: queue => calls.Add($"reload:{ReferenceEquals(queue, reviewQueue)}"),
                LoadSamplesInternalAsync: () =>
                {
                    calls.Add("load-samples-internal");
                    return Task.CompletedTask;
                },
                RefreshKbStatusAsync: () =>
                {
                    calls.Add("refresh-kb");
                    return Task.CompletedTask;
                },
                ResetVisuals: () => calls.Add("reset-visuals"),
                CancellationToken: tokenSource.Token),
            new SelfTrainingRunRequestFactoryDefaults(
                BeginActivity: () =>
                {
                    calls.Add("activity-start");
                    return new TrackingDisposable(calls, "activity-dispose");
                },
                PrepareRuntimeAsync: (getHttp, setHttp, appSettings, codeCatalog, log) =>
                {
                    calls.Add($"prepare:{ReferenceEquals(getHttp(), cachedHttpClient)}:{appSettings is null}:{codeCatalog is null}");
                    setHttp(cachedHttpClient);
                    log("runtime-log");
                    return Task.FromResult(setup);
                },
                AppendHistoryAsync: snapshot =>
                {
                    calls.Add($"history:{snapshot.CaseId}");
                    return Task.CompletedTask;
                },
                LoadSamplesAsync: () =>
                {
                    calls.Add("load-samples");
                    return Task.FromResult(loadedSamples);
                },
                MergeOrUpdateSamplesAsync: samples =>
                {
                    calls.Add($"merge:{samples.Count()}");
                    return Task.CompletedTask;
                },
                UtcNow: () => DateTime.UnixEpoch));

        Assert.Same(selectedCase, request.SelectedCase);
        Assert.Same(reviewQueue, request.ReviewQueueService);
        Assert.Equal(tokenSource.Token, request.CancellationToken);
        Assert.Equal(DateTime.UnixEpoch, request.UtcNow());

        request.Ui.SetBusy(true);
        request.Ui.SetSelfTrainingRunning(true);
        request.Ui.SetLogText("");
        request.Ui.SetStatusText("laeuft");
        request.Ui.Log("meldung");
        using (request.BeginActivity())
        {
        }
        Assert.Same(setup, await request.PrepareRuntimeAsync(request.Ui.Log));
        request.SetActiveVisionModel("vision");
        request.SetOrchestrator(setup.Session.Orchestrator);
        request.OnProgress(new SelfTrainingStep(0, 1, "BAA", 1.2, SelfTrainingStage.Analyzing, null, null, null));
        await request.AppendHistoryAsync(new SelfTrainingRunSnapshot(DateTime.UnixEpoch, "case-1", 1, 1, 0, 0, 0));
        await request.UpdateKbAsync(
            new SelfTrainingResult("case-1", 1, 1, 0, 0, 0, null, TimeSpan.FromSeconds(1), 1),
            tokenSource.Token);
        Assert.Same(loadedSamples, await request.LoadSamplesAsync());
        request.ReloadReviewQueue(reviewQueue);
        await request.LoadSamplesInternalAsync();
        await request.RefreshKbStatusAsync();
        request.ResetVisuals();

        Assert.Contains("busy:True", calls);
        Assert.Contains("running:True", calls);
        Assert.Contains("log-text:", calls);
        Assert.Contains("status:laeuft", calls);
        Assert.Contains("log:meldung", calls);
        Assert.Contains("activity-start", calls);
        Assert.Contains("activity-dispose", calls);
        Assert.Contains("prepare:True:True:True", calls);
        Assert.Contains("set-http", calls);
        Assert.Contains("log:runtime-log", calls);
        Assert.Contains("active-model:vision", calls);
        Assert.Contains("orchestrator:FakeSelfTrainingOrchestrator", calls);
        Assert.Contains("progress", calls);
        Assert.Contains("history:case-1", calls);
        Assert.Contains("load-samples", calls);
        Assert.Contains("merge:1", calls);
        Assert.Contains("index:1:True", calls);
        Assert.Contains("reload:True", calls);
        Assert.Contains("load-samples-internal", calls);
        Assert.Contains("refresh-kb", calls);
        Assert.Contains("reset-visuals", calls);
        Assert.Equal(KbIndexState.Indexed, loadedSamples[0].KbIndexState);
    }

    private static SelfTrainingRuntimeSetup CreateSetup(List<string> calls)
        => new(
            RuntimeSettings(),
            RetrievalConfig(),
            new TrainingCenterSettings(),
            new HttpClient(),
            new SelfTrainingSession(
                "vision-active",
                new FakeSelfTrainingOrchestrator(),
                [new TrackingDisposable(calls, "session-dispose")]));

    private static AiRuntimeSettings RuntimeSettings()
        => new(
            Enabled: true,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision",
            TextModel: "text",
            EmbedModel: "embed",
            FfmpegPath: "ffmpeg",
            OllamaRequestTimeout: TimeSpan.FromMinutes(2),
            OllamaKeepAlive: "5m",
            OllamaNumCtx: 2048);

    private static OllamaConfig RetrievalConfig()
        => new(
            new Uri("http://localhost:11434"),
            "vision",
            "text",
            "embed",
            TimeSpan.FromMinutes(2));

    private sealed class FakeSelfTrainingOrchestrator : ISelfTrainingOrchestrator
    {
        public bool IsPaused { get; private set; }

        public Task<SelfTrainingResult> RunAsync(
            TrainingCaseInput tc,
            IProgress<SelfTrainingStep> progress,
            CancellationToken ct)
            => Task.FromResult(new SelfTrainingResult(tc.CaseId, 0, 0, 0, 0, 0, null, TimeSpan.Zero, 0));

        public void Pause() => IsPaused = true;

        public void Resume() => IsPaused = false;
    }

    private sealed class TrackingDisposable(List<string> calls, string marker) : IDisposable
    {
        public void Dispose() => calls.Add(marker);
    }
}
