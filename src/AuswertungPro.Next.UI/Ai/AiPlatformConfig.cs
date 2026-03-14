using System;
using System.Globalization;
using AuswertungPro.Next.UI.Ai.Ollama;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Einheitliche KI-Plattformkonfiguration.
/// Ladestrategie: AppSettings (nullable) > Env-Var > Default.
/// </summary>
public sealed record AiPlatformConfig(
    // ── Flags ──
    bool Enabled,

    // ── Ollama ──
    Uri OllamaBaseUri,
    string VisionModel,
    string TextModel,
    string EmbedModel,
    TimeSpan OllamaRequestTimeout,
    string OllamaKeepAlive,
    int OllamaNumCtx,

    // ── Pipeline / Sidecar ──
    bool MultiModelEnabled,
    Uri SidecarUrl,
    PipelineMode PipelineMode,
    double YoloConfidence,
    double DinoBoxThreshold,
    double DinoTextThreshold,
    int SidecarTimeoutSec,
    int? PipeDiameterMmOverride,

    // ── Tools ──
    string FfmpegPath
)
{
    // ── Projektionen ────────────────────────────────────────────────────

    public AiRuntimeConfig ToRuntimeConfig() => new(
        Enabled:              Enabled,
        OllamaBaseUri:        OllamaBaseUri,
        VisionModel:          VisionModel,
        TextModel:            TextModel,
        EmbedModel:           EmbedModel,
        FfmpegPath:           FfmpegPath,
        OllamaRequestTimeout: OllamaRequestTimeout,
        OllamaKeepAlive:      OllamaKeepAlive,
        OllamaNumCtx:         OllamaNumCtx);

    public OllamaConfig ToOllamaConfig() => new(
        BaseUri:        OllamaBaseUri,
        VisionModel:    VisionModel,
        TextModel:      TextModel,
        EmbedModel:     EmbedModel,
        RequestTimeout: OllamaRequestTimeout,
        KeepAlive:      OllamaKeepAlive,
        NumCtx:         OllamaNumCtx);

    public PipelineConfig ToPipelineConfig() => new(
        MultiModelEnabled:      MultiModelEnabled,
        SidecarUrl:             SidecarUrl,
        Mode:                   PipelineMode,
        YoloConfidence:         YoloConfidence,
        DinoBoxThreshold:       DinoBoxThreshold,
        DinoTextThreshold:      DinoTextThreshold,
        SidecarTimeoutSec:      SidecarTimeoutSec,
        PipeDiameterMmOverride: PipeDiameterMmOverride);

    // ── Laden ───────────────────────────────────────────────────────────

    /// <summary>Lädt Konfiguration (AppSettings werden intern geladen).</summary>
    public static AiPlatformConfig Load()
    {
        AppSettings? settings = null;
        try { settings = AppSettings.Load(); } catch { /* ignore */ }
        return Load(settings);
    }

    /// <summary>Lädt Konfiguration mit vorgeladenen AppSettings (vermeidet Doppel-Load).</summary>
    public static AiPlatformConfig Load(AppSettings? settings)
    {
        // ── Flags ──
        var enabled = settings?.AiEnabled
            ?? ParseBool(Env("SEWERSTUDIO_AI_ENABLED"));

        // ── Ollama ──
        var ollamaUrl = FirstNonEmpty(
                settings?.AiOllamaUrl,
                Env("SEWERSTUDIO_OLLAMA_URL"))
            ?? "http://localhost:11434";

        var vision = FirstNonEmpty(
                settings?.AiVisionModel,
                Env("SEWERSTUDIO_AI_VISION_MODEL"))
            ?? OllamaConfig.DefaultVisionModel;

        var text = FirstNonEmpty(
                settings?.AiTextModel,
                Env("SEWERSTUDIO_AI_TEXT_MODEL"))
            ?? OllamaConfig.DefaultTextModel;

        var embed = FirstNonEmpty(
                settings?.AiEmbedModel,
                Env("SEWERSTUDIO_AI_EMBED_MODEL"))
            ?? OllamaConfig.DefaultEmbedModel;

        var timeoutMin = settings?.AiOllamaTimeoutMin
            ?? ParseInt(Env("SEWERSTUDIO_AI_TIMEOUT_MIN"))
            ?? 5;

        var keepAlive = FirstNonEmpty(
                settings?.AiOllamaKeepAlive,
                Env("SEWERSTUDIO_OLLAMA_KEEP_ALIVE"))
            ?? OllamaConfig.DefaultKeepAlive;

        var numCtx = settings?.AiOllamaNumCtx
            ?? ParseInt(Env("SEWERSTUDIO_OLLAMA_NUM_CTX"))
            ?? OllamaConfig.DefaultNumCtx;

        // ── Pipeline ──
        var multiModelEnabled = settings?.PipelineMultiModelEnabled
            ?? ParseBool(Env("SEWERSTUDIO_MULTIMODEL_ENABLED"));

        var sidecarUrl = FirstNonEmpty(
                settings?.PipelineSidecarUrl,
                Env("SEWERSTUDIO_SIDECAR_URL"))
            ?? "http://localhost:8100";

        var modeStr = FirstNonEmpty(
                settings?.PipelineMode,
                Env("SEWERSTUDIO_PIPELINE_MODE"))
            ?? "ollamaonly";
        var mode = modeStr.Trim().ToLowerInvariant() switch
        {
            "multimodel" or "multi" => PipelineMode.MultiModel,
            "ollama" or "ollamaonly" => PipelineMode.OllamaOnly,
            _ => PipelineMode.Auto,
        };

        var yoloConf = settings?.PipelineYoloConfidence
            ?? ParseDouble(Env("SEWERSTUDIO_YOLO_CONFIDENCE"))
            ?? 0.25;

        var dinoBox = settings?.PipelineDinoBoxThreshold
            ?? ParseDouble(Env("SEWERSTUDIO_DINO_BOX_THRESHOLD"))
            ?? 0.30;

        var dinoText = settings?.PipelineDinoTextThreshold
            ?? ParseDouble(Env("SEWERSTUDIO_DINO_TEXT_THRESHOLD"))
            ?? 0.25;

        var sidecarTimeout = ParseInt(Env("SEWERSTUDIO_SIDECAR_TIMEOUT_SEC")) ?? 300;

        var pipeDiameter = settings?.PipelinePipeDiameterMm
            ?? ParseInt(Env("SEWERSTUDIO_PIPE_DIAMETER_MM"));

        // ── Tools ──
        var ffmpeg = FirstNonEmpty(
                settings?.AiFfmpegPath,
                Env("SEWERSTUDIO_FFMPEG"))
            ?? "ffmpeg";

        return new AiPlatformConfig(
            Enabled:                enabled,
            OllamaBaseUri:          new Uri(ollamaUrl),
            VisionModel:            vision,
            TextModel:              text,
            EmbedModel:             embed,
            OllamaRequestTimeout:   TimeSpan.FromMinutes(timeoutMin),
            OllamaKeepAlive:        keepAlive,
            OllamaNumCtx:           numCtx,
            MultiModelEnabled:      multiModelEnabled,
            SidecarUrl:             new Uri(sidecarUrl),
            PipelineMode:           mode,
            YoloConfidence:         yoloConf,
            DinoBoxThreshold:       dinoBox,
            DinoTextThreshold:      dinoText,
            SidecarTimeoutSec:      sidecarTimeout,
            PipeDiameterMmOverride: pipeDiameter,
            FfmpegPath:             ffmpeg);
    }

    // ── Parse-Helpers ───────────────────────────────────────────────────

    public static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return trimmed == "1"
            || (bool.TryParse(trimmed, out var parsed) && parsed);
    }

    public static double? ParseDouble(string? value) =>
        double.TryParse(value?.Trim(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var d) ? d : null;

    public static int? ParseInt(string? value) =>
        int.TryParse(value?.Trim(), out var i) ? i : null;

    private static string? Env(string name)
    {
        var val = Environment.GetEnvironmentVariable(name)?.Trim();
        if (!string.IsNullOrEmpty(val)) return val;
        // Backward compat: fall back to legacy AUSWERTUNGPRO_ prefix
        if (name.StartsWith("SEWERSTUDIO_", StringComparison.Ordinal))
            return Environment.GetEnvironmentVariable(
                "AUSWERTUNGPRO_" + name["SEWERSTUDIO_".Length..])?.Trim();
        return null;
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
