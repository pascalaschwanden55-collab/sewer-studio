using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AiServiceFactoryTests
{
    [Fact]
    public void Videoanalyse_Fabrik_erzeugt_Pipeline_mit_aktueller_Konfiguration()
    {
        var configReads = 0;
        var pipelineConfig = CreatePipelineConfig();
        var factory = new VideoAnalysisPipelineFactory(() =>
        {
            configReads++;
            return pipelineConfig;
        });

        using var httpClient = new HttpClient();
        var service = factory.Create(
            CreateRuntimeSettings(),
            new RuleBasedAiSuggestionPlausibilityService(),
            httpClient);

        Assert.IsType<VideoAnalysisPipelineService>(service);
        Assert.Equal(1, configReads);
    }

    [Fact]
    public void Sanierungs_Fabrik_erzeugt_Optimierungsdienst()
    {
        var factory = new AiSanierungOptimizationFactory();

        var service = factory.Create(CreateRuntimeSettings());

        Assert.IsType<AiSanierungOptimizationService>(service);
    }

    private static AiRuntimeSettings CreateRuntimeSettings()
        => new(
            Enabled: true,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision",
            TextModel: "text",
            EmbedModel: null,
            FfmpegPath: "ffmpeg",
            OllamaRequestTimeout: TimeSpan.FromSeconds(30),
            OllamaKeepAlive: "5m",
            OllamaNumCtx: 2048);

    private static PipelineConfig CreatePipelineConfig()
        => new(
            MultiModelEnabled: true,
            SidecarUrl: new Uri("http://localhost:8100"),
            SidecarToken: null,
            Mode: PipelineMode.Auto,
            YoloConfidence: 0.25,
            YoloClassConfidence: new Dictionary<string, double>(),
            DinoBoxThreshold: 0.3,
            DinoTextThreshold: 0.25,
            SidecarTimeoutSec: 30,
            PipeDiameterMmOverride: null);
}
