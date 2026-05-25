using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class AiSettingsTests
{
    [Fact]
    public void AiSettingsModels_LiveInApplicationLayer()
    {
        Assert.StartsWith("AuswertungPro.Next.Application", typeof(AiRuntimeSettings).Namespace);
        Assert.StartsWith("AuswertungPro.Next.Application", typeof(AiPlatformSettings).Namespace);
        Assert.StartsWith("AuswertungPro.Next.Application", typeof(AiSettingsSource).Namespace);
        Assert.StartsWith("AuswertungPro.Next.Application", typeof(IAiSettingsProvider).Namespace);
    }

    [Fact]
    public void AiPlatformSettings_ProjectsRuntimeAndPipelineSettings()
    {
        var platform = new AiPlatformSettings(
            Enabled: true,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "qwen2.5vl:7b",
            TextModel: "qwen2.5:14b",
            EmbedModel: "nomic-embed-text",
            OllamaRequestTimeout: TimeSpan.FromMinutes(5),
            OllamaKeepAlive: "24h",
            OllamaNumCtx: 8192,
            MultiModelEnabled: true,
            SidecarUrl: new Uri("http://localhost:8100"),
            PipelineMode: PipelineMode.Auto,
            YoloConfidence: 0.25,
            YoloClassConfidence: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["BAB"] = 0.15
            },
            DinoBoxThreshold: 0.30,
            DinoTextThreshold: 0.25,
            SidecarTimeoutSec: 300,
            PipeDiameterMmOverride: 300,
            FfmpegPath: "ffmpeg");

        var runtime = platform.ToRuntimeSettings();
        var pipeline = platform.ToPipelineConfig();

        Assert.True(runtime.Enabled);
        Assert.Equal("qwen2.5vl:7b", runtime.VisionModel);
        Assert.Equal("qwen2.5:14b", runtime.TextModel);
        Assert.Equal("ffmpeg", runtime.FfmpegPath);
        Assert.True(pipeline.MultiModelEnabled);
        Assert.Equal(PipelineMode.Auto, pipeline.Mode);
        Assert.Equal(0.25, pipeline.YoloConfidence);
    }

    [Fact]
    public void AiSettingsFactory_UsesSourceOverEnvironmentAndDefaults()
    {
        using var env = new AiSettingsEnvScope();
        env.Set("SEWERSTUDIO_SIDECAR_TIMEOUT_SEC", "999");

        var source = new AiSettingsSource(
            Enabled: true,
            OllamaUrl: "http://127.0.0.1:11434",
            VisionModel: "custom-vision",
            TextModel: "custom-text",
            EmbedModel: "custom-embed",
            OllamaTimeoutMin: 9,
            OllamaKeepAlive: "12h",
            OllamaNumCtx: 4096,
            MultiModelEnabled: true,
            SidecarUrl: "http://127.0.0.1:8100",
            PipelineMode: "multi",
            YoloConfidence: 0.42,
            DinoBoxThreshold: 0.50,
            DinoTextThreshold: 0.60,
            PipeDiameterMm: 250,
            FfmpegPath: @"C:\tools\ffmpeg.exe");

        var settings = AiSettingsFactory.Load(source);

        Assert.True(settings.Enabled);
        Assert.Equal(new Uri("http://127.0.0.1:11434"), settings.OllamaBaseUri);
        Assert.Equal("custom-vision", settings.VisionModel);
        Assert.Equal("custom-text", settings.TextModel);
        Assert.Equal("custom-embed", settings.EmbedModel);
        Assert.Equal(TimeSpan.FromMinutes(9), settings.OllamaRequestTimeout);
        Assert.Equal("12h", settings.OllamaKeepAlive);
        Assert.Equal(4096, settings.OllamaNumCtx);
        Assert.True(settings.MultiModelEnabled);
        Assert.Equal(new Uri("http://127.0.0.1:8100"), settings.SidecarUrl);
        Assert.Equal(PipelineMode.MultiModel, settings.PipelineMode);
        Assert.Equal(0.42, settings.YoloConfidence);
        Assert.Equal(0.50, settings.DinoBoxThreshold);
        Assert.Equal(0.60, settings.DinoTextThreshold);
        Assert.Equal(250, settings.PipeDiameterMmOverride);
        Assert.Equal(@"C:\tools\ffmpeg.exe", settings.FfmpegPath);
    }

    [Theory]
    [InlineData("multimodel", PipelineMode.MultiModel)]
    [InlineData("multi", PipelineMode.MultiModel)]
    [InlineData("ollama", PipelineMode.OllamaOnly)]
    [InlineData("ollamaonly", PipelineMode.OllamaOnly)]
    [InlineData("auto", PipelineMode.Auto)]
    [InlineData("unknown", PipelineMode.OllamaOnly)]
    [InlineData(null, PipelineMode.OllamaOnly)]
    public void AiSettingsFactory_ParsesPipelineMode(string? value, PipelineMode expected)
    {
        using var env = new AiSettingsEnvScope();

        var settings = AiSettingsFactory.Load(new AiSettingsSource(PipelineMode: value));

        Assert.Equal(expected, settings.PipelineMode);
    }

    [Fact]
    public void AiPlatformSettings_MapsToInfrastructureOllamaConfig()
    {
        using var env = new AiSettingsEnvScope();
        var platform = AiSettingsFactory.Load(new AiSettingsSource(
            OllamaUrl: "http://127.0.0.1:11434",
            VisionModel: "vision",
            TextModel: "text",
            EmbedModel: "embed",
            OllamaTimeoutMin: 7,
            OllamaKeepAlive: "6h",
            OllamaNumCtx: 4096));

        var ollama = platform.ToOllamaConfig();

        Assert.Equal(new Uri("http://127.0.0.1:11434"), ollama.BaseUri);
        Assert.Equal("vision", ollama.VisionModel);
        Assert.Equal("text", ollama.TextModel);
        Assert.Equal("embed", ollama.EmbedModel);
        Assert.Equal(TimeSpan.FromMinutes(7), ollama.RequestTimeout);
        Assert.Equal("6h", ollama.KeepAlive);
        Assert.Equal(4096, ollama.NumCtx);
    }

    private sealed class AiSettingsEnvScope : IDisposable
    {
        private static readonly string[] Keys =
        [
            "SEWERSTUDIO_AI_ENABLED",
            "SEWERSTUDIO_OLLAMA_URL",
            "SEWERSTUDIO_AI_VISION_MODEL",
            "SEWERSTUDIO_AI_TEXT_MODEL",
            "SEWERSTUDIO_AI_EMBED_MODEL",
            "SEWERSTUDIO_AI_TIMEOUT_MIN",
            "SEWERSTUDIO_OLLAMA_KEEP_ALIVE",
            "SEWERSTUDIO_OLLAMA_NUM_CTX",
            "SEWERSTUDIO_MULTIMODEL_ENABLED",
            "SEWERSTUDIO_SIDECAR_URL",
            "SEWERSTUDIO_PIPELINE_MODE",
            "SEWERSTUDIO_YOLO_CONFIDENCE",
            "SEWERSTUDIO_DINO_BOX_THRESHOLD",
            "SEWERSTUDIO_DINO_TEXT_THRESHOLD",
            "SEWERSTUDIO_SIDECAR_TIMEOUT_SEC",
            "SEWERSTUDIO_PIPE_DIAMETER_MM",
            "SEWERSTUDIO_FFMPEG"
        ];

        private static readonly string[] LegacyKeys = Keys
            .Select(static key => "AUSWERTUNGPRO_" + key["SEWERSTUDIO_".Length..])
            .ToArray();

        private readonly Dictionary<string, string?> _backup = new(StringComparer.Ordinal);

        public AiSettingsEnvScope()
        {
            foreach (var key in Keys.Concat(LegacyKeys))
            {
                _backup[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, null);
            }
        }

        public void Set(string key, string? value) =>
            Environment.SetEnvironmentVariable(key, value);

        public void Dispose()
        {
            foreach (var pair in _backup)
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }
}
