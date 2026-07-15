using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPipelineHealthControllerTests
{
    [Fact]
    public async Task InitializeAsync_applies_disabled_runtime_and_disables_analysis()
    {
        var runtimeController = new CodingAiController();
        var runtime = DisabledRuntime();
        var states = new List<string>();
        var controller = new CodingPipelineHealthController(
            runtimeController,
            Actions(
                createRuntime: () => runtime,
                setCodingAiState: (status, _, detail) => states.Add($"{status}|{detail}"),
                setAnalyzeButtonEnabled: enabled => states.Add($"analyze:{enabled}")));

        await controller.InitializeAsync();

        Assert.Equal(runtime.ModelName, runtimeController.ModelName);
        Assert.False(runtimeController.AiEnabled);
        Assert.Equal(
            [
                "Künstliche Intelligenz deaktiviert|Modell: aus",
                "analyze:False"
            ],
            states);
    }

    [Fact]
    public async Task InitializeAsync_wires_status_changes_dispatches_rechecks_state_and_stops_monitor()
    {
        var runtimeController = new CodingAiController();
        var monitor = new FakePipelineHealthMonitor();
        var isClosing = false;
        Action? dispatched = null;
        var calls = new List<string>();
        var controller = new CodingPipelineHealthController(
            runtimeController,
            Actions(
                createRuntime: EnabledRuntime,
                createHealthMonitor: _ => monitor,
                isClosing: () => isClosing,
                hasDispatcherAccess: () => false,
                dispatchToUi: action => dispatched = action,
                setCodingAiState: (status, _, detail) => calls.Add($"state:{status}|{detail}"),
                setAnalyzeButtonEnabled: enabled => calls.Add($"analyze:{enabled}"),
                updatePipelineHealthDetails: details => calls.Add($"details:{details.Mode}")));

        await controller.InitializeAsync();

        Assert.True(monitor.Started);
        Assert.True(runtimeController.HasHealthMonitor);
        calls.Clear();
        monitor.RaiseStatusChanged(DownStatus());

        Assert.NotNull(dispatched);
        Assert.Empty(calls);

        isClosing = true;
        dispatched();
        Assert.Empty(calls);

        isClosing = false;
        monitor.RaiseStatusChanged(DownStatus());
        dispatched!();

        Assert.Equal(
            [
                "state:aus|offline",
                "analyze:False",
                "details:Modus: KI aus"
            ],
            calls);
        Assert.False(runtimeController.UseMultiModel);

        controller.Stop();

        Assert.True(monitor.Stopped);
        Assert.False(runtimeController.HasHealthMonitor);
        Assert.False(runtimeController.AiEnabled);
    }

    private static CodingPipelineHealthControllerActions Actions(
        Func<CodingAiRuntime>? createRuntime = null,
        Func<CodingAiRuntime, IPipelineHealthMonitor>? createHealthMonitor = null,
        Func<bool>? isClosing = null,
        Func<bool>? hasDispatcherAccess = null,
        Action<Action>? dispatchToUi = null,
        Action<string, System.Windows.Media.Color, string?>? setCodingAiState = null,
        Action<bool>? setAnalyzeButtonEnabled = null,
        Action<PipelineHealthDetailsUiState>? updatePipelineHealthDetails = null)
        => new(
            CreateRuntime: createRuntime ?? (() => throw new InvalidOperationException("CreateRuntime should not run.")),
            CreateHealthMonitor: createHealthMonitor ?? (_ => throw new InvalidOperationException("CreateHealthMonitor should not run.")),
            IsClosing: isClosing ?? (() => false),
            DispatcherHasShutdownStarted: () => false,
            HasDispatcherAccess: hasDispatcherAccess ?? (() => true),
            IsCodingMode: () => true,
            DispatchToUi: dispatchToUi ?? (_ => throw new InvalidOperationException("DispatchToUi should not run.")),
            SetCodingAiState: setCodingAiState ?? ((_, _, _) => { }),
            SetAnalyzeButtonEnabled: setAnalyzeButtonEnabled ?? (_ => { }),
            SetYoloStatus: (_, _, _) => { },
            UpdatePipelineHealthDetails: updatePipelineHealthDetails ?? (_ => { }));

    private static CodingAiRuntime DisabledRuntime()
        => new(
            new AiRuntimeSettings(
                Enabled: false,
                new Uri("http://127.0.0.1:11434"),
                VisionModel: "vision-test:latest",
                TextModel: "text-test:latest",
                EmbedModel: "embed-test:latest",
                FfmpegPath: null,
                OllamaRequestTimeout: TimeSpan.FromSeconds(30),
                OllamaKeepAlive: "24h",
                OllamaNumCtx: 4096),
            new PipelineConfig(
                MultiModelEnabled: true,
                new Uri("http://127.0.0.1:8100"),
                SidecarToken: null,
                PipelineMode.Auto,
                YoloConfidence: 0.35,
                YoloClassConfidence: new Dictionary<string, double>(),
                DinoBoxThreshold: 0.3,
                DinoTextThreshold: 0.25,
                SidecarTimeoutSec: 60,
                PipeDiameterMmOverride: 300),
            ModelName: "vision-test:latest",
            LiveDetection: null,
            EnhancedVision: null,
            QualityGate: null,
            ProtocolVerifier: null,
            VisionClient: null,
            MultiModel: null,
            BoxSegmentation: null,
            MultiModelError: null);

    private static CodingAiRuntime EnabledRuntime()
    {
        var disabled = DisabledRuntime();
        var visionClient = new VisionPipelineClient(disabled.PipelineConfig.SidecarUrl);
        return disabled with
        {
            RuntimeSettings = disabled.RuntimeSettings with { Enabled = true },
            VisionClient = visionClient,
            MultiModel = new SingleFrameMultiModelService(visionClient, disabled.PipelineConfig),
            BoxSegmentation = new MarkBoxSegmentationService(
                (_, _) => throw new InvalidOperationException("Segmentation should not run."))
        };
    }

    private static PipelineHealthStatus DownStatus()
        => new(
            PipelineHealthLevel.Down,
            MultiModelActive: false,
            SidecarReachable: false,
            TokenValid: false,
            SidecarHealthy: false,
            QwenAvailable: false,
            YoloLoaded: false,
            DinoLoaded: false,
            SamLoaded: false,
            Summary: "aus",
            Detail: "offline");

    private sealed class FakePipelineHealthMonitor : IPipelineHealthMonitor
    {
        public PipelineHealthStatus CurrentStatus => DownStatus();
        public bool Started { get; private set; }
        public bool Stopped { get; private set; }

        public event EventHandler<PipelineHealthStatus>? StatusChanged;

        public void RaiseStatusChanged(PipelineHealthStatus status)
            => StatusChanged?.Invoke(this, status);

        public void Start()
        {
            Started = true;
        }

        public Task StopAsync()
        {
            Stopped = true;
            return Task.CompletedTask;
        }

        public Task<PipelineHealthStatus> RefreshOnceAsync(CancellationToken ct = default)
            => Task.FromResult(CurrentStatus);

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
