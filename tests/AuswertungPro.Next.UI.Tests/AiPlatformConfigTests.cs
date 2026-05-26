using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AuswertungPro.Next.UI.Tests;

public sealed class AiSettingsFactoryTests
{
    [Fact]
    public void Load_WithoutEnvOrSettings_ReturnsDefaults()
    {
        using var env = new EnvVarScope();

        var config = AiSettingsFactory.Load();
        var expectedVision = GpuModelSelector.DetectAndSelect()?.ResolvedModel
            ?? OllamaConfig.DefaultVisionModel;

        Assert.False(config.Enabled);
        Assert.Equal(new Uri("http://localhost:11434"), config.OllamaBaseUri);
        Assert.Equal(expectedVision, config.VisionModel);
        Assert.Equal(OllamaConfig.DefaultTextModel, config.TextModel);
        Assert.Equal(OllamaConfig.DefaultEmbedModel, config.EmbedModel);
        Assert.Equal(TimeSpan.FromMinutes(5), config.OllamaRequestTimeout);
        Assert.False(config.MultiModelEnabled);
        Assert.Equal(new Uri("http://localhost:8100"), config.SidecarUrl);
        Assert.Equal(PipelineMode.OllamaOnly, config.PipelineMode);
        Assert.Equal(0.25, config.YoloConfidence);
        Assert.Equal(0.30, config.DinoBoxThreshold);
        Assert.Equal(0.25, config.DinoTextThreshold);
        Assert.Equal(300, config.SidecarTimeoutSec);
        Assert.Null(config.PipeDiameterMmOverride);
        Assert.Equal("ffmpeg", config.FfmpegPath);
    }

    [Fact]
    public void Load_UsesEnvVars_WhenSettingsAbsent()
    {
        using var env = new EnvVarScope();
        env.Set("SEWERSTUDIO_AI_ENABLED", "1");
        env.Set("SEWERSTUDIO_OLLAMA_URL", "http://127.0.0.1:11435");
        env.Set("SEWERSTUDIO_AI_VISION_MODEL", "vision-x");
        env.Set("SEWERSTUDIO_AI_TEXT_MODEL", "text-x");
        env.Set("SEWERSTUDIO_AI_EMBED_MODEL", "embed-x");
        env.Set("SEWERSTUDIO_AI_TIMEOUT_MIN", "42");
        env.Set("SEWERSTUDIO_OLLAMA_KEEP_ALIVE", "12h");
        env.Set("SEWERSTUDIO_OLLAMA_NUM_CTX", "4096");
        env.Set("SEWERSTUDIO_MULTIMODEL_ENABLED", "true");
        env.Set("SEWERSTUDIO_SIDECAR_URL", "http://127.0.0.1:8101");
        env.Set("SEWERSTUDIO_PIPELINE_MODE", "multi");
        env.Set("SEWERSTUDIO_YOLO_CONFIDENCE", "0.55");
        env.Set("SEWERSTUDIO_DINO_BOX_THRESHOLD", "0.45");
        env.Set("SEWERSTUDIO_DINO_TEXT_THRESHOLD", "0.35");
        env.Set("SEWERSTUDIO_SIDECAR_TIMEOUT_SEC", "222");
        env.Set("SEWERSTUDIO_PIPE_DIAMETER_MM", "500");
        env.Set("SEWERSTUDIO_FFMPEG", @"C:\tools\ffmpeg.exe");

        var config = AiSettingsFactory.Load();

        Assert.True(config.Enabled);
        Assert.Equal(new Uri("http://127.0.0.1:11435"), config.OllamaBaseUri);
        Assert.Equal("vision-x", config.VisionModel);
        Assert.Equal("text-x", config.TextModel);
        Assert.Equal("embed-x", config.EmbedModel);
        Assert.Equal(TimeSpan.FromMinutes(42), config.OllamaRequestTimeout);
        Assert.Equal("12h", config.OllamaKeepAlive);
        Assert.Equal(4096, config.OllamaNumCtx);
        Assert.True(config.MultiModelEnabled);
        Assert.Equal(new Uri("http://127.0.0.1:8101"), config.SidecarUrl);
        Assert.Equal(PipelineMode.MultiModel, config.PipelineMode);
        Assert.Equal(0.55, config.YoloConfidence);
        Assert.Equal(0.45, config.DinoBoxThreshold);
        Assert.Equal(0.35, config.DinoTextThreshold);
        Assert.Equal(222, config.SidecarTimeoutSec);
        Assert.Equal(500, config.PipeDiameterMmOverride);
        Assert.Equal(@"C:\tools\ffmpeg.exe", config.FfmpegPath);
    }

    [Fact]
    public void Load_AppSettingsOverrideEnvVars()
    {
        using var env = new EnvVarScope();
        env.Set("SEWERSTUDIO_AI_ENABLED", "0");
        env.Set("SEWERSTUDIO_OLLAMA_URL", "http://env-host:11434");
        env.Set("SEWERSTUDIO_AI_VISION_MODEL", "env-vision");
        env.Set("SEWERSTUDIO_AI_TEXT_MODEL", "env-text");
        env.Set("SEWERSTUDIO_AI_EMBED_MODEL", "env-embed");
        env.Set("SEWERSTUDIO_AI_TIMEOUT_MIN", "15");
        env.Set("SEWERSTUDIO_MULTIMODEL_ENABLED", "0");
        env.Set("SEWERSTUDIO_SIDECAR_URL", "http://env-sidecar:8100");
        env.Set("SEWERSTUDIO_PIPELINE_MODE", "ollama");
        env.Set("SEWERSTUDIO_YOLO_CONFIDENCE", "0.10");
        env.Set("SEWERSTUDIO_DINO_BOX_THRESHOLD", "0.11");
        env.Set("SEWERSTUDIO_DINO_TEXT_THRESHOLD", "0.12");
        env.Set("SEWERSTUDIO_SIDECAR_TIMEOUT_SEC", "77");
        env.Set("SEWERSTUDIO_PIPE_DIAMETER_MM", "250");
        env.Set("SEWERSTUDIO_FFMPEG", "env-ffmpeg");

        var settings = new AppSettings
        {
            AiEnabled = true,
            AiOllamaUrl = "http://settings-host:11436",
            AiVisionModel = "settings-vision",
            AiTextModel = "settings-text",
            AiEmbedModel = "settings-embed",
            AiOllamaTimeoutMin = 99,
            AiFfmpegPath = "settings-ffmpeg",
            PipelineMultiModelEnabled = true,
            PipelineSidecarUrl = "http://settings-sidecar:8102",
            PipelineMode = "multi",
            PipelineYoloConfidence = 0.61,
            PipelineDinoBoxThreshold = 0.62,
            PipelineDinoTextThreshold = 0.63,
            PipelinePipeDiameterMm = 700
        };

        var config = AiSettingsFactory.Load(AppSettingsAiSettingsProvider.ToSource(settings));

        Assert.True(config.Enabled);
        Assert.Equal(new Uri("http://settings-host:11436"), config.OllamaBaseUri);
        Assert.Equal("settings-vision", config.VisionModel);
        Assert.Equal("settings-text", config.TextModel);
        Assert.Equal("settings-embed", config.EmbedModel);
        Assert.Equal(TimeSpan.FromMinutes(99), config.OllamaRequestTimeout);
        Assert.True(config.MultiModelEnabled);
        Assert.Equal(new Uri("http://settings-sidecar:8102"), config.SidecarUrl);
        Assert.Equal(PipelineMode.MultiModel, config.PipelineMode);
        Assert.Equal(0.61, config.YoloConfidence);
        Assert.Equal(0.62, config.DinoBoxThreshold);
        Assert.Equal(0.63, config.DinoTextThreshold);
        Assert.Equal(77, config.SidecarTimeoutSec);
        Assert.Equal(700, config.PipeDiameterMmOverride);
        Assert.Equal("settings-ffmpeg", config.FfmpegPath);
    }

    [Fact]
    public void Load_NullSettings_FallsBackToEnvVars()
    {
        using var env = new EnvVarScope();
        env.Set("SEWERSTUDIO_AI_ENABLED", "1");
        env.Set("SEWERSTUDIO_AI_TEXT_MODEL", "env-only-text");
        env.Set("SEWERSTUDIO_PIPELINE_MODE", "ollamaonly");

        var config = AiSettingsFactory.Load();

        Assert.True(config.Enabled);
        Assert.Equal("env-only-text", config.TextModel);
        Assert.Equal(PipelineMode.OllamaOnly, config.PipelineMode);
    }

    [Fact]
    public void ToRuntimeSettings_ProjectsCorrectly()
    {
        var config = CreateConfig();

        var runtime = config.ToRuntimeSettings();

        Assert.Equal(new AiRuntimeSettings(
            Enabled: true,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision-model",
            TextModel: "text-model",
            EmbedModel: "embed-model",
            FfmpegPath: "ffmpeg-custom",
            OllamaRequestTimeout: TimeSpan.FromMinutes(12),
            OllamaKeepAlive: "24h",
            OllamaNumCtx: 8192), runtime);
    }

    [Fact]
    public void ToOllamaConfig_ProjectsCorrectly()
    {
        var config = CreateConfig();

        var ollama = config.ToOllamaConfig();

        Assert.Equal(new OllamaConfig(
            BaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision-model",
            TextModel: "text-model",
            EmbedModel: "embed-model",
            RequestTimeout: TimeSpan.FromMinutes(12),
            KeepAlive: "24h"), ollama);
    }

    [Fact]
    public void ToPipelineConfig_ProjectsCorrectly()
    {
        var config = CreateConfig();

        var pipeline = config.ToPipelineConfig();

        Assert.Equal(0.44, pipeline.YoloConfidence);
        Assert.Equal(0.45, pipeline.DinoBoxThreshold);
        Assert.Equal(0.46, pipeline.DinoTextThreshold);
        Assert.Equal(123, pipeline.SidecarTimeoutSec);
        Assert.Equal(600, pipeline.PipeDiameterMmOverride);
        Assert.NotNull(pipeline.YoloClassConfidence);
        Assert.True(pipeline.YoloClassConfidence.Count > 0);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("yes", false)]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData(" True ", true)]
    public void ParseBool_HandlesEdgeCases(string? value, bool expected)
    {
        Assert.Equal(expected, AiSettingsFactory.ParseBool(value));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("0.25", 0.25)]
    [InlineData(" 1.5 ", 1.5)]
    [InlineData("-3.75", -3.75)]
    [InlineData("1,25", null)]
    [InlineData("abc", null)]
    public void ParseDouble_HandlesEdgeCases(string? value, double? expected)
    {
        Assert.Equal(expected, AiSettingsFactory.ParseDouble(value));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("42", 42)]
    [InlineData(" -7 ", -7)]
    [InlineData("7.5", null)]
    [InlineData("abc", null)]
    public void ParseInt_HandlesEdgeCases(string? value, int? expected)
    {
        Assert.Equal(expected, AiSettingsFactory.ParseInt(value));
    }

    [Fact]
    public void AppSettingsProvider_Load_MatchesFactoryProjection()
    {
        using var settings = new SettingsFileScope();
        using var env = new EnvVarScope();
        env.Set("SEWERSTUDIO_AI_ENABLED", "1");
        env.Set("SEWERSTUDIO_OLLAMA_URL", "http://127.0.0.1:22400");
        env.Set("SEWERSTUDIO_AI_VISION_MODEL", "vision-wrapper");
        env.Set("SEWERSTUDIO_AI_TEXT_MODEL", "text-wrapper");
        env.Set("SEWERSTUDIO_AI_EMBED_MODEL", "embed-wrapper");
        env.Set("SEWERSTUDIO_FFMPEG", "wrapper-ffmpeg");

        var actual = new AppSettingsAiSettingsProvider().Load().ToRuntimeSettings();
        var expected = AiSettingsFactory.Load().ToRuntimeSettings();

        Assert.Equal(expected, actual);
    }

    private static AiPlatformSettings CreateConfig() => new(
        Enabled: true,
        OllamaBaseUri: new Uri("http://localhost:11434"),
        VisionModel: "vision-model",
        TextModel: "text-model",
        EmbedModel: "embed-model",
        OllamaRequestTimeout: TimeSpan.FromMinutes(12),
        OllamaKeepAlive: "24h",
        OllamaNumCtx: 8192,
        MultiModelEnabled: true,
        SidecarUrl: new Uri("http://localhost:8100"),
        PipelineMode: PipelineMode.MultiModel,
        YoloConfidence: 0.44,
        YoloClassConfidence: new Dictionary<string, double> { ["BAB"] = 0.15, ["BCA"] = 0.40 },
        DinoBoxThreshold: 0.45,
        DinoTextThreshold: 0.46,
        SidecarTimeoutSec: 123,
        PipeDiameterMmOverride: 600,
        FfmpegPath: "ffmpeg-custom");

    private sealed class EnvVarScope : IDisposable
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
            .Select(static k => "AUSWERTUNGPRO_" + k["SEWERSTUDIO_".Length..])
            .ToArray();

        private readonly Dictionary<string, string?> _backup = new(StringComparer.Ordinal);

        public EnvVarScope()
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

    private sealed class SettingsFileScope : IDisposable
    {
        private readonly string? _previousAppDataDir;
        private readonly string _tempAppDataDir;

        public SettingsFileScope()
        {
            _previousAppDataDir = Environment.GetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR");
            _tempAppDataDir = Path.Combine(
                Path.GetTempPath(),
                "sewerstudio-ai-config-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempAppDataDir);
            Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", _tempAppDataDir);
        }

        public void Dispose()
        {
            try
            {
                Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", _previousAppDataDir);
                if (Directory.Exists(_tempAppDataDir))
                    Directory.Delete(_tempAppDataDir, recursive: true);
            }
            catch
            {
                // Test cleanup should not mask assertions.
            }
        }
    }
}
