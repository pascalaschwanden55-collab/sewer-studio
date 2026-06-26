using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiHealthMonitorCreationWorkflowTests
{
    [Fact]
    public void Create_passes_runtime_vision_client_and_status_delegates_to_factory()
    {
        var calls = new List<string>();
        var visionClient = new FakeVisionPipelineClient();
        var monitor = new FakePipelineHealthMonitor();
        var runtime = Runtime(visionClient);

        var result = CodingAiHealthMonitorCreationWorkflow.Create(
            new CodingAiHealthMonitorCreationRequest(
                runtime,
                AiEnabled: () =>
                {
                    calls.Add("ai");
                    return true;
                },
                QwenAvailable: () =>
                {
                    calls.Add("qwen");
                    return false;
                }),
            new CodingAiHealthMonitorCreationActions(
                CreateHealthMonitor: (client, aiEnabled, qwenAvailable) =>
                {
                    calls.Add("create");
                    Assert.Same(visionClient, client);
                    Assert.True(aiEnabled());
                    Assert.False(qwenAvailable());
                    return monitor;
                }));

        Assert.Same(monitor, result);
        Assert.Equal(["create", "ai", "qwen"], calls);
    }

    private static CodingAiRuntime Runtime(IVisionPipelineClient visionClient)
        => new(
            new AiRuntimeSettings(
                Enabled: true,
                OllamaBaseUri: new Uri("http://127.0.0.1:11434"),
                VisionModel: "vision-test",
                TextModel: "text-test",
                EmbedModel: "embed-test",
                FfmpegPath: null,
                OllamaRequestTimeout: TimeSpan.FromSeconds(30),
                OllamaKeepAlive: "24h",
                OllamaNumCtx: 4096),
            new PipelineConfig(
                MultiModelEnabled: true,
                SidecarUrl: new Uri("http://127.0.0.1:8100"),
                SidecarToken: null,
                Mode: PipelineMode.Auto,
                YoloConfidence: 0.25,
                YoloClassConfidence: new Dictionary<string, double>(),
                DinoBoxThreshold: 0.25,
                DinoTextThreshold: 0.2,
                SidecarTimeoutSec: 300,
                PipeDiameterMmOverride: null),
            ModelName: "vision-test",
            LiveDetection: null,
            EnhancedVision: null,
            QualityGate: null,
            VisionClient: visionClient,
            MultiModel: null,
            BoxSegmentation: null,
            MultiModelError: null);

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

        public event EventHandler<PipelineHealthStatus>? StatusChanged
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public Task<PipelineHealthStatus> RefreshOnceAsync(CancellationToken ct = default)
            => Task.FromResult(CurrentStatus);

        public Task StopAsync()
            => Task.CompletedTask;

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
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
