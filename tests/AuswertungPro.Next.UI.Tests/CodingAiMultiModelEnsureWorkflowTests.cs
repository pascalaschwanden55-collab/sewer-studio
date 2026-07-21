using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiMultiModelEnsureWorkflowTests
{
    [Fact]
    public void Ensure_delegates_service_creation_to_controller()
    {
        var calls = new List<string>();
        var controller = new CodingAiController();
        var visionClient = new FakeVisionPipelineClient();
        var pipelineConfig = PipelineSettings();
        var expectedService = new SingleFrameMultiModelService(visionClient, pipelineConfig);

        controller.ApplyRuntime(Runtime(visionClient, pipelineConfig));

        var result = CodingAiMultiModelEnsureWorkflow.Ensure(
            controller,
            new CodingAiMultiModelEnsureActions(
                CreateMultiModelService: (client, config) =>
                {
                    calls.Add("create");
                    Assert.Same(visionClient, client);
                    Assert.Same(pipelineConfig, config);
                    return expectedService;
                }));

        Assert.Same(expectedService, result);
        Assert.Same(expectedService, controller.MultiModel);
        Assert.Equal(["create"], calls);
    }

    private static CodingAiRuntime Runtime(
        IVisionPipelineClient visionClient,
        PipelineConfig pipelineConfig)
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
            pipelineConfig,
            ModelName: "vision-test",
            LiveDetection: null,
            EnhancedVision: null,
            QualityGate: null,
            ProtocolVerifier: null,
            VisionClient: visionClient,
            MultiModel: null,
            BoxSegmentation: null,
            MultiModelError: null);

    private static PipelineConfig PipelineSettings()
        => new(
            MultiModelEnabled: true,
            SidecarUrl: new Uri("http://127.0.0.1:8100"),
            SidecarToken: null,
            Mode: PipelineMode.Auto,
            YoloConfidence: 0.25,
            YoloClassConfidence: new Dictionary<string, double>(),
            DinoBoxThreshold: 0.25,
            DinoTextThreshold: 0.2,
            SidecarTimeoutSec: 300,
            PipeDiameterMmOverride: null);

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

    }
}
