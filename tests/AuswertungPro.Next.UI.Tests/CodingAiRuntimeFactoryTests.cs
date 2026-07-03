using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiRuntimeFactoryTests
{
    [Fact]
    public void Create_disabled_runtime_does_not_create_services()
    {
        var runtime = CodingAiRuntimeFactory.Create(CreateSettings(enabled: false), codeCatalog: null);

        Assert.False(runtime.RuntimeSettings.Enabled);
        Assert.Null(runtime.LiveDetection);
        Assert.Null(runtime.EnhancedVision);
        Assert.Null(runtime.QualityGate);
        Assert.Null(runtime.ProtocolVerifier);
        Assert.Null(runtime.VisionClient);
        Assert.Null(runtime.MultiModel);
        Assert.Null(runtime.BoxSegmentation);
        Assert.Null(runtime.MultiModelError);
    }

    [Fact]
    public void Create_enabled_runtime_uses_override_pipeline_config()
    {
        var platformSettings = CreateSettings(enabled: true);
        var overrideConfig = platformSettings.ToPipelineConfig() with
        {
            SidecarUrl = new Uri("http://127.0.0.1:9010"),
            SidecarToken = "override-token"
        };

        var runtime = CodingAiRuntimeFactory.Create(platformSettings, codeCatalog: null, overrideConfig);

        Assert.True(runtime.RuntimeSettings.Enabled);
        Assert.Same(overrideConfig, runtime.PipelineConfig);
        Assert.Equal("vision-test:latest", runtime.ModelName);
        Assert.NotNull(runtime.LiveDetection);
        Assert.NotNull(runtime.EnhancedVision);
        Assert.NotNull(runtime.QualityGate);
        Assert.NotNull(runtime.ProtocolVerifier);
        Assert.NotNull(runtime.VisionClient);
        Assert.NotNull(runtime.MultiModel);
        Assert.NotNull(runtime.BoxSegmentation);
        Assert.Null(runtime.MultiModelError);
    }

    private static AiPlatformSettings CreateSettings(bool enabled)
        => new(
            Enabled: enabled,
            OllamaBaseUri: new Uri("http://127.0.0.1:11434"),
            VisionModel: "vision-test:latest",
            TextModel: "text-test:latest",
            EmbedModel: "embed-test:latest",
            OllamaRequestTimeout: TimeSpan.FromSeconds(30),
            OllamaKeepAlive: "24h",
            OllamaNumCtx: 4096,
            MultiModelEnabled: true,
            SidecarUrl: new Uri("http://127.0.0.1:8000"),
            SidecarToken: "token",
            PipelineMode: PipelineMode.Auto,
            YoloConfidence: 0.35,
            YoloClassConfidence: new Dictionary<string, double>(),
            DinoBoxThreshold: 0.3,
            DinoTextThreshold: 0.25,
            SidecarTimeoutSec: 60,
            PipeDiameterMmOverride: 300,
            FfmpegPath: "ffmpeg");
}
