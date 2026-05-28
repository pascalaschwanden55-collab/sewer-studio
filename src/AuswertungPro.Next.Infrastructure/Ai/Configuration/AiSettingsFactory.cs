using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.Infrastructure.Ai.Configuration;

public static class AiSettingsFactory
{
    public static AiPlatformSettings Load(AiSettingsSource? source = null)
    {
        source ??= new AiSettingsSource();

        var configuredVision = FirstNonEmpty(
            source.VisionModel,
            Env("SEWERSTUDIO_AI_VISION_MODEL"));

        string vision;
        var numCtxDefault = OllamaConfig.DefaultNumCtx;

        if (GpuModelSelector.IsAutoMode(configuredVision))
        {
            var gpuProfile = GpuModelSelector.DetectAndSelect();
            if (gpuProfile is not null)
            {
                vision = gpuProfile.ResolvedModel;
                numCtxDefault = gpuProfile.ResolvedNumCtx;
                Debug.WriteLine($"[AiSettingsFactory] GPU Auto-Select: {gpuProfile.Reason}");
            }
            else
            {
                vision = OllamaConfig.DefaultVisionModel;
            }
        }
        else
        {
            vision = configuredVision!;
        }

        var yoloClassConf = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["BAB"] = 0.15,
            ["BAA"] = 0.20,
            ["BAC"] = 0.25,
            ["BBA"] = 0.20,
            ["BBB"] = 0.25,
            ["BBC"] = 0.25,
            ["BCA"] = 0.35,
            ["BCC"] = 0.30,
            ["BCD"] = 0.30,
            ["BCE"] = 0.30
        };

        return new AiPlatformSettings(
            Enabled: source.Enabled ?? ParseBool(Env("SEWERSTUDIO_AI_ENABLED")),
            OllamaBaseUri: new Uri(FirstNonEmpty(source.OllamaUrl, Env("SEWERSTUDIO_OLLAMA_URL")) ?? "http://localhost:11434"),
            VisionModel: vision,
            TextModel: FirstNonEmpty(source.TextModel, Env("SEWERSTUDIO_AI_TEXT_MODEL")) ?? OllamaConfig.DefaultTextModel,
            EmbedModel: FirstNonEmpty(source.EmbedModel, Env("SEWERSTUDIO_AI_EMBED_MODEL")) ?? OllamaConfig.DefaultEmbedModel,
            OllamaRequestTimeout: TimeSpan.FromMinutes(source.OllamaTimeoutMin ?? ParseInt(Env("SEWERSTUDIO_AI_TIMEOUT_MIN")) ?? 5),
            OllamaKeepAlive: FirstNonEmpty(source.OllamaKeepAlive, Env("SEWERSTUDIO_OLLAMA_KEEP_ALIVE")) ?? OllamaConfig.DefaultKeepAlive,
            OllamaNumCtx: source.OllamaNumCtx ?? ParseInt(Env("SEWERSTUDIO_OLLAMA_NUM_CTX")) ?? numCtxDefault,
            MultiModelEnabled: source.MultiModelEnabled ?? ParseBool(Env("SEWERSTUDIO_MULTIMODEL_ENABLED")),
            SidecarUrl: new Uri(FirstNonEmpty(source.SidecarUrl, Env("SEWERSTUDIO_SIDECAR_URL")) ?? "http://localhost:8100"),
            SidecarToken: FirstNonEmpty(
                source.SidecarToken,
                Env("SEWERSTUDIO_SIDECAR_TOKEN"),
                RawEnv("SEWER_SIDECAR_AUTH_TOKEN"),
                RawEnv("SEWER_SIDECAR_TOKEN")),
            PipelineMode: ParsePipelineMode(FirstNonEmpty(source.PipelineMode, Env("SEWERSTUDIO_PIPELINE_MODE"))),
            YoloConfidence: source.YoloConfidence ?? ParseDouble(Env("SEWERSTUDIO_YOLO_CONFIDENCE")) ?? 0.25,
            YoloClassConfidence: yoloClassConf,
            DinoBoxThreshold: source.DinoBoxThreshold ?? ParseDouble(Env("SEWERSTUDIO_DINO_BOX_THRESHOLD")) ?? 0.30,
            DinoTextThreshold: source.DinoTextThreshold ?? ParseDouble(Env("SEWERSTUDIO_DINO_TEXT_THRESHOLD")) ?? 0.25,
            SidecarTimeoutSec: ParseInt(Env("SEWERSTUDIO_SIDECAR_TIMEOUT_SEC")) ?? 300,
            PipeDiameterMmOverride: source.PipeDiameterMm ?? ParseInt(Env("SEWERSTUDIO_PIPE_DIAMETER_MM")),
            FfmpegPath: FirstNonEmpty(source.FfmpegPath, Env("SEWERSTUDIO_FFMPEG")) ?? "ffmpeg");
    }

    public static PipelineMode ParsePipelineMode(string? value)
    {
        return (value ?? "ollamaonly").Trim().ToLowerInvariant() switch
        {
            "multimodel" or "multi" => PipelineMode.MultiModel,
            "ollama" or "ollamaonly" => PipelineMode.OllamaOnly,
            "auto" => PipelineMode.Auto,
            _ => PipelineMode.OllamaOnly
        };
    }

    public static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return trimmed == "1" || (bool.TryParse(trimmed, out var parsed) && parsed);
    }

    public static double? ParseDouble(string? value) =>
        double.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    public static int? ParseInt(string? value) =>
        int.TryParse(value?.Trim(), out var parsed) ? parsed : null;

    private static string? Env(string name)
    {
        var value = Environment.GetEnvironmentVariable(name)?.Trim();
        if (!string.IsNullOrEmpty(value))
            return value;

        if (name.StartsWith("SEWERSTUDIO_", StringComparison.Ordinal))
        {
            return Environment.GetEnvironmentVariable(
                "AUSWERTUNGPRO_" + name["SEWERSTUDIO_".Length..])?.Trim();
        }

        return null;
    }

    private static string? RawEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name)?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
