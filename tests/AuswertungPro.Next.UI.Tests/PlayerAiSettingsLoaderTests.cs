using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerAiSettingsLoaderTests
{
    [Fact]
    public void LoadRuntimeSettings_uses_supplied_provider()
    {
        var runtime = PlayerAiSettingsLoader.LoadRuntimeSettings(new FakeProvider());

        Assert.True(runtime.Enabled);
        Assert.Equal("vision-test", runtime.VisionModel);
        Assert.Equal(new Uri("http://127.0.0.1:11434"), runtime.OllamaBaseUri);
    }

    [Fact]
    public void LoadPlatformSettings_uses_supplied_provider()
    {
        var platform = PlayerAiSettingsLoader.LoadPlatformSettings(new FakeProvider());

        Assert.True(platform.Enabled);
        Assert.Equal("vision-test", platform.VisionModel);
        Assert.Equal(PipelineMode.Auto, platform.PipelineMode);
    }

    private sealed class FakeProvider : IAiSettingsProvider
    {
        public AiPlatformSettings Load()
            => new(
                Enabled: true,
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
    }
}
