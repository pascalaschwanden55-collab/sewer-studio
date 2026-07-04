using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunWorkflowTests
{
    [Fact]
    public async Task RunAsync_fuehrt_selbsttraining_aus_und_finalisiert_ui()
    {
        var calls = new List<string>();
        var result = new SelfTrainingResult(
            CaseId: "case-1",
            TotalEntries: 2,
            ExactMatches: 1,
            PartialMatches: 1,
            Mismatches: 0,
            NoFindings: 0,
            OverallTechnique: null,
            Duration: TimeSpan.FromSeconds(8),
            SamplesGenerated: 2);
        var orchestrator = new FakeSelfTrainingOrchestrator(result, calls);
        var request = CreateRequest(calls, orchestrator);

        await SelfTrainingRunWorkflow.RunAsync(request);

        Assert.Contains("busy:True", calls);
        Assert.Contains("running:True", calls);
        Assert.Contains("reset-visuals", calls);
        Assert.Contains("prepare-runtime", calls);
        Assert.Contains("active-model:vision-active", calls);
        Assert.Contains("set-orchestrator:FakeSelfTrainingOrchestrator", calls);
        Assert.Contains("orchestrator-run:case-1", calls);
        Assert.Contains("history:case-1:2", calls);
        Assert.Contains("kb-update:case-1", calls);
        Assert.Contains("load-samples-internal", calls);
        Assert.Contains("refresh-kb", calls);
        Assert.Equal("running:False", calls[^3]);
        Assert.Equal("set-orchestrator:null", calls[^2]);
        Assert.Equal("activity-dispose", calls[^1]);
    }

    [Fact]
    public async Task RunAsync_loggt_fehler_und_finalisiert_ui()
    {
        var calls = new List<string>();
        var orchestrator = new FakeSelfTrainingOrchestrator(
            new SelfTrainingResult("case-1", 0, 0, 0, 0, 0, null, TimeSpan.Zero, 0),
            calls)
        {
            ExceptionToThrow = new InvalidOperationException("kaputt")
        };
        var request = CreateRequest(calls, orchestrator);

        await SelfTrainingRunWorkflow.RunAsync(request);

        Assert.Contains("log:FEHLER: InvalidOperationException: kaputt", calls);
        Assert.Contains("status:Fehler: kaputt", calls);
        Assert.Equal("busy:False", calls[^4]);
        Assert.Equal("running:False", calls[^3]);
        Assert.Equal("set-orchestrator:null", calls[^2]);
        Assert.Equal("activity-dispose", calls[^1]);
    }

    private static SelfTrainingRunWorkflowRequest CreateRequest(
        List<string> calls,
        ISelfTrainingOrchestrator orchestrator)
    {
        var selectedCase = new TrainingCase
        {
            CaseId = "case-1",
            FolderPath = "folder",
            VideoPath = "video.mp4",
            ProtocolPath = "protocol.pdf"
        };
        var ui = new SelfTrainingUiSink(
            value => calls.Add($"busy:{value}"),
            value => calls.Add($"running:{value}"),
            value => calls.Add($"log-text:{value}"),
            value => calls.Add($"status:{value}"),
            value => calls.Add($"log:{value}"));

        return new SelfTrainingRunWorkflowRequest(
            SelectedCase: selectedCase,
            Ui: ui,
            BeginActivity: () =>
            {
                calls.Add("activity-start");
                return new TrackingDisposable(calls, "activity-dispose");
            },
            PrepareRuntimeAsync: _ =>
            {
                calls.Add("prepare-runtime");
                return Task.FromResult(CreateSetup(orchestrator, calls));
            },
            SetActiveVisionModel: value => calls.Add($"active-model:{value}"),
            SetOrchestrator: value => calls.Add($"set-orchestrator:{value?.GetType().Name ?? "null"}"),
            OnProgress: _ => calls.Add("progress"),
            AppendHistoryAsync: snapshot =>
            {
                calls.Add($"history:{snapshot.CaseId}:{snapshot.TotalEntries}");
                return Task.CompletedTask;
            },
            UpdateKbAsync: (runResult, _) =>
            {
                calls.Add($"kb-update:{runResult.CaseId}");
                return Task.CompletedTask;
            },
            ReviewQueueService: null,
            LoadSamplesAsync: () =>
            {
                calls.Add("load-samples");
                return Task.FromResult(new List<TrainingSample>());
            },
            ReloadReviewQueue: _ => calls.Add("reload-review"),
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
            UtcNow: () => DateTime.UnixEpoch,
            CancellationToken: CancellationToken.None);
    }

    private static SelfTrainingRuntimeSetup CreateSetup(
        ISelfTrainingOrchestrator orchestrator,
        List<string> calls)
        => new(
            RuntimeSettings(),
            RetrievalConfig(),
            new TrainingCenterSettings(),
            new HttpClient(),
            CreateSession(orchestrator, calls));

    private static AiRuntimeSettings RuntimeSettings()
        => new(
            Enabled: true,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision-active",
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

    private static SelfTrainingSession CreateSession(
        ISelfTrainingOrchestrator orchestrator,
        List<string> calls)
        => new(
            "vision-active",
            orchestrator,
            new[] { new TrackingDisposable(calls, "session-dispose") });

    private sealed class FakeSelfTrainingOrchestrator(
        SelfTrainingResult result,
        List<string> calls) : ISelfTrainingOrchestrator
    {
        public Exception? ExceptionToThrow { get; init; }

        public bool IsPaused { get; private set; }

        public Task<SelfTrainingResult> RunAsync(
            TrainingCaseInput tc,
            IProgress<SelfTrainingStep> progress,
            CancellationToken ct)
        {
            calls.Add($"orchestrator-run:{tc.CaseId}");
            if (ExceptionToThrow is not null)
                throw ExceptionToThrow;

            return Task.FromResult(result);
        }

        public void Pause() => IsPaused = true;

        public void Resume() => IsPaused = false;
    }

    private sealed class TrackingDisposable(List<string> calls, string marker) : IDisposable
    {
        public void Dispose() => calls.Add(marker);
    }
}
