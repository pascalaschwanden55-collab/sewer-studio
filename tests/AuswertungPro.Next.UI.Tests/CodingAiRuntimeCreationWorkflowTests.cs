using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiRuntimeCreationWorkflowTests
{
    [Fact]
    public void Create_loads_platform_settings_and_creates_runtime()
    {
        var calls = new List<string>();
        var settings = PlatformSettings();
        var pipelineConfig = PipelineSettings();
        var expectedRuntime = new CodingAiRuntime(
            settings.ToRuntimeSettings(),
            pipelineConfig,
            settings.VisionModel,
            LiveDetection: null,
            EnhancedVision: null,
            QualityGate: null,
            ProtocolVerifier: null,
            VisionClient: null,
            MultiModel: null,
            BoxSegmentation: null,
            MultiModelError: null);

        var runtime = CodingAiRuntimeCreationWorkflow.Create(
            new CodingAiRuntimeCreationRequest(
                CodeCatalog: null,
                PipelineConfig: pipelineConfig),
            new CodingAiRuntimeCreationActions(
                LoadPlatformSettings: () =>
                {
                    calls.Add("load");
                    return settings;
                },
                CreateRuntime: (loadedSettings, codeCatalog, loadedPipelineConfig) =>
                {
                    calls.Add("create");
                    Assert.Same(settings, loadedSettings);
                    Assert.Null(codeCatalog);
                    Assert.Same(pipelineConfig, loadedPipelineConfig);
                    return expectedRuntime;
                }));

        Assert.Same(expectedRuntime, runtime);
        Assert.Equal(["load", "create"], calls);
    }

    private static AiPlatformSettings PlatformSettings()
        => new(
            Enabled: false,
            OllamaBaseUri: new Uri("http://127.0.0.1:11434"),
            VisionModel: "vision-test",
            TextModel: "text-test",
            EmbedModel: "embed-test",
            OllamaRequestTimeout: TimeSpan.FromSeconds(30),
            OllamaKeepAlive: "24h",
            OllamaNumCtx: 4096,
            MultiModelEnabled: true,
            SidecarUrl: new Uri("http://127.0.0.1:8100"),
            SidecarToken: "token",
            PipelineMode: PipelineMode.Auto,
            YoloConfidence: 0.25,
            YoloClassConfidence: new Dictionary<string, double>(),
            DinoBoxThreshold: 0.25,
            DinoTextThreshold: 0.2,
            SidecarTimeoutSec: 300,
            PipeDiameterMmOverride: null,
            FfmpegPath: "ffmpeg");

    private static PipelineConfig PipelineSettings()
        => new(
            MultiModelEnabled: true,
            SidecarUrl: new Uri("http://127.0.0.1:8100"),
            SidecarToken: "token",
            Mode: PipelineMode.Auto,
            YoloConfidence: 0.25,
            YoloClassConfidence: new Dictionary<string, double>(),
            DinoBoxThreshold: 0.25,
            DinoTextThreshold: 0.2,
            SidecarTimeoutSec: 300,
            PipeDiameterMmOverride: null);
}
