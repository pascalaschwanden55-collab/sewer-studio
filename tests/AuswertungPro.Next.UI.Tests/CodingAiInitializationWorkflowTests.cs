using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiInitializationWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_disables_ai_and_analyze_button_when_runtime_is_disabled()
    {
        var calls = new List<string>();
        var runtime = Runtime(enabled: false);

        var result = await CodingAiInitializationWorkflow.ExecuteAsync(Actions(
            calls,
            CreateRuntime: () =>
            {
                calls.Add("create");
                return runtime;
            },
            ApplyRuntime: value =>
            {
                Assert.Same(runtime, value);
                calls.Add("apply");
            },
            SetCodingAiState: (text, color, detail) =>
            {
                Assert.Equal("Künstliche Intelligenz deaktiviert", text);
                Assert.Equal(PlayerStatusColors.Muted, color);
                Assert.Equal("Modell: aus", detail);
                calls.Add("state:disabled");
            },
            SetAnalyzeButtonEnabled: enabled => calls.Add($"analyze:{enabled}")));

        Assert.Equal(CodingAiInitializationWorkflowOutcome.Disabled, result.Outcome);
        Assert.Equal(
            [
                "create",
                "apply",
                "state:disabled",
                "analyze:False"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_starts_health_monitor_refreshes_once_and_sets_yolo_status_for_multimodel_runtime()
    {
        var calls = new List<string>();
        var runtime = Runtime(enabled: true, multiModelAvailable: true);
        var monitor = new FakePipelineHealthMonitor();

        var result = await CodingAiInitializationWorkflow.ExecuteAsync(Actions(
            calls,
            CreateRuntime: () =>
            {
                calls.Add("create");
                return runtime;
            },
            ApplyRuntime: value =>
            {
                Assert.Same(runtime, value);
                calls.Add("apply");
            },
            CreateHealthMonitor: value =>
            {
                Assert.Same(runtime, value);
                calls.Add("monitor:create");
                return monitor;
            },
            StartHealthMonitor: value =>
            {
                Assert.Same(monitor, value);
                calls.Add("monitor:start");
            },
            RefreshHealthOnceAsync: () =>
            {
                calls.Add("refresh");
                return Task.FromResult(monitor.CurrentStatus);
            },
            ApplyPipelineHealth: status =>
            {
                Assert.Same(monitor.CurrentStatus, status);
                calls.Add($"health:{status.Level}");
            },
            GetModelName: () =>
            {
                calls.Add("model");
                return runtime.ModelName;
            },
            SetYoloStatus: (text, color, model) =>
            {
                Assert.Equal("Bereit", text);
                Assert.Equal(PlayerStatusColors.Success, color);
                Assert.Equal("vision-test:latest", model);
                calls.Add("yolo");
            }));

        Assert.Equal(CodingAiInitializationWorkflowOutcome.MultiModelReady, result.Outcome);
        Assert.Equal(
            [
                "create",
                "apply",
                "monitor:create",
                "monitor:start",
                "refresh",
                "health:Full",
                "model",
                "yolo"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_reports_qwen_fallback_when_multimodel_runtime_has_error()
    {
        var calls = new List<string>();
        var runtime = Runtime(enabled: true, multiModelError: "sidecar offline");

        var result = await CodingAiInitializationWorkflow.ExecuteAsync(Actions(
            calls,
            CreateRuntime: () =>
            {
                calls.Add("create");
                return runtime;
            },
            ApplyRuntime: value =>
            {
                Assert.Same(runtime, value);
                calls.Add("apply");
            },
            SetUseMultiModel: enabled => calls.Add($"multimodel:{enabled}"),
            SetCodingAiState: (text, color, detail) =>
            {
                Assert.Equal("Künstliche Intelligenz bereit (Qwen)", text);
                Assert.Equal(PlayerStatusColors.Success, color);
                Assert.Equal("Monitor-Fehler: sidecar offline", detail);
                calls.Add("state:qwen");
            },
            GetModelName: () =>
            {
                calls.Add("model");
                return runtime.ModelName;
            },
            SetYoloStatus: (text, color, model) =>
            {
                Assert.Equal("Bereit", text);
                Assert.Equal(PlayerStatusColors.Success, color);
                Assert.Equal("vision-test:latest", model);
                calls.Add("yolo");
            }));

        Assert.Equal(CodingAiInitializationWorkflowOutcome.QwenFallback, result.Outcome);
        Assert.Equal(
            [
                "create",
                "apply",
                "multimodel:False",
                "state:qwen",
                "model",
                "yolo"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_reports_error_and_disables_analyze_when_initialization_fails()
    {
        var calls = new List<string>();

        var result = await CodingAiInitializationWorkflow.ExecuteAsync(Actions(
            calls,
            CreateRuntime: () => throw new InvalidOperationException("boom"),
            GetModelName: () =>
            {
                calls.Add("model");
                return "models/qwen2.5-vl:7b";
            },
            SetCodingAiState: (text, color, detail) =>
            {
                Assert.Equal("Fehler: boom", text);
                Assert.Equal(PlayerStatusColors.Error, color);
                Assert.Equal("Modell: qwen2.5-vl:7b", detail);
                calls.Add("state:error");
            },
            SetAnalyzeButtonEnabled: enabled => calls.Add($"analyze:{enabled}")));

        Assert.Equal(CodingAiInitializationWorkflowOutcome.Failed, result.Outcome);
        Assert.Equal(
            [
                "model",
                "state:error",
                "analyze:False"
            ],
            calls);
    }

    private static CodingAiInitializationWorkflowActions Actions(
        List<string> calls,
        Func<CodingAiRuntime>? CreateRuntime = null,
        Action<CodingAiRuntime>? ApplyRuntime = null,
        Func<CodingAiRuntime, IPipelineHealthMonitor>? CreateHealthMonitor = null,
        Action<IPipelineHealthMonitor>? StartHealthMonitor = null,
        Func<Task<PipelineHealthStatus>>? RefreshHealthOnceAsync = null,
        Action<PipelineHealthStatus>? ApplyPipelineHealth = null,
        Action<string, System.Windows.Media.Color, string?>? SetCodingAiState = null,
        Action<bool>? SetAnalyzeButtonEnabled = null,
        Action<bool>? SetUseMultiModel = null,
        Func<string>? GetModelName = null,
        Action<string, System.Windows.Media.Color, string?>? SetYoloStatus = null)
        => new(
            CreateRuntime: CreateRuntime ?? (() => throw new InvalidOperationException("CreateRuntime should not run.")),
            ApplyRuntime: ApplyRuntime ?? (_ => throw new InvalidOperationException("ApplyRuntime should not run.")),
            CreateHealthMonitor: CreateHealthMonitor ?? (_ => throw new InvalidOperationException("CreateHealthMonitor should not run.")),
            StartHealthMonitor: StartHealthMonitor ?? (_ => throw new InvalidOperationException("StartHealthMonitor should not run.")),
            RefreshHealthOnceAsync: RefreshHealthOnceAsync ?? (() => throw new InvalidOperationException("RefreshHealthOnceAsync should not run.")),
            ApplyPipelineHealth: ApplyPipelineHealth ?? (_ => throw new InvalidOperationException("ApplyPipelineHealth should not run.")),
            SetCodingAiState: SetCodingAiState ?? ((_, _, _) => throw new InvalidOperationException("SetCodingAiState should not run.")),
            SetAnalyzeButtonEnabled: SetAnalyzeButtonEnabled ?? (_ => throw new InvalidOperationException("SetAnalyzeButtonEnabled should not run.")),
            SetUseMultiModel: SetUseMultiModel ?? (_ => throw new InvalidOperationException("SetUseMultiModel should not run.")),
            GetModelName: GetModelName ?? (() => throw new InvalidOperationException("GetModelName should not run.")),
            SetYoloStatus: SetYoloStatus ?? ((_, _, _) => throw new InvalidOperationException("SetYoloStatus should not run.")));

    private static CodingAiRuntime Runtime(
        bool enabled,
        bool multiModelAvailable = false,
        string? multiModelError = null)
    {
        var pipelineConfig = new PipelineConfig(
            MultiModelEnabled: true,
            new Uri("http://127.0.0.1:8100"),
            SidecarToken: null,
            PipelineMode.Auto,
            YoloConfidence: 0.35,
            YoloClassConfidence: new Dictionary<string, double>(),
            DinoBoxThreshold: 0.3,
            DinoTextThreshold: 0.25,
            SidecarTimeoutSec: 60,
            PipeDiameterMmOverride: 300);

        var visionClient = multiModelAvailable ? new FakeVisionPipelineClient() : null;
        var multiModel = visionClient is null ? null : new SingleFrameMultiModelService(visionClient, pipelineConfig);
        var boxSegmentation = visionClient is null
            ? null
            : new MarkBoxSegmentationService((_, _) => throw new NotImplementedException());

        return new CodingAiRuntime(
            new AiRuntimeSettings(
                enabled,
                new Uri("http://127.0.0.1:11434"),
                "vision-test:latest",
                "text-test:latest",
                "embed-test:latest",
                FfmpegPath: null,
                TimeSpan.FromSeconds(30),
                "24h",
                4096),
            pipelineConfig,
            "vision-test:latest",
            LiveDetection: null,
            EnhancedVision: null,
            QualityGate: null,
            VisionClient: visionClient,
            MultiModel: multiModel,
            BoxSegmentation: boxSegmentation,
            MultiModelError: multiModelError);
    }

    private sealed class FakePipelineHealthMonitor : IPipelineHealthMonitor
    {
        public PipelineHealthStatus CurrentStatus { get; } = new(
            PipelineHealthLevel.Full,
            MultiModelActive: true,
            SidecarReachable: true,
            TokenValid: true,
            SidecarHealthy: true,
            QwenAvailable: true,
            YoloLoaded: true,
            DinoLoaded: true,
            SamLoaded: true,
            Summary: "ok",
            Detail: "ready");

        public event EventHandler<PipelineHealthStatus>? StatusChanged;

        public void Start()
        {
        }

        public Task StopAsync()
            => Task.CompletedTask;

        public Task<PipelineHealthStatus> RefreshOnceAsync(CancellationToken ct = default)
            => Task.FromResult(CurrentStatus);

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        public void RaiseStatus()
            => StatusChanged?.Invoke(this, CurrentStatus);
    }

    private sealed class FakeVisionPipelineClient : IVisionPipelineClient
    {
        public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<TrainingExportResponseDto> ExportTrainingAsync(TrainingExportRequestDto request, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
