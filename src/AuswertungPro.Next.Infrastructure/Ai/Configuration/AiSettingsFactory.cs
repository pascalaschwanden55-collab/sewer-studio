using System.Diagnostics;
using System.Globalization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.Infrastructure.Ai.Configuration;

/// <summary>Liest KI-Laufzeitwerte ueber injizierbare Systemquellen.</summary>
public sealed class AiPlatformSettingsResolver : IAiPlatformSettingsResolver
{
    private readonly IGpuModelSelector _gpuModels;
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Action<string> _writeTrace;

    public AiPlatformSettingsResolver(
        IGpuModelSelector gpuModels,
        Func<string, string?>? getEnvironmentVariable = null,
        Action<string>? writeTrace = null)
    {
        _gpuModels = gpuModels ?? throw new ArgumentNullException(nameof(gpuModels));
        _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        _writeTrace = writeTrace ?? (message => Trace.WriteLine(message));
    }

    public AiPlatformSettings Load(AiSettingsSource? source = null)
    {
        source ??= new AiSettingsSource();

        var configuredVision = FirstNonEmpty(
            source.VisionModel,
            Env("SEWERSTUDIO_AI_VISION_MODEL"));

        string vision;
        var numCtxDefault = OllamaConfig.DefaultNumCtx;

        if (GpuModelSelector.IsAutoMode(configuredVision))
        {
            var gpuProfile = _gpuModels.DetectAndSelect();
            if (gpuProfile is not null)
            {
                vision = gpuProfile.ResolvedModel;
                numCtxDefault = gpuProfile.ResolvedNumCtx;
                _writeTrace($"[AiSettingsFactory] GPU Auto-Select: {gpuProfile.Reason}");
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
            // Seit 2026-06-18: 0.30 laesst relevante Anschluss-Frames mit
            // Confidence 0.30-0.34 weiterhin in DINO/SAM einlaufen.
            ["BCA"] = 0.30,
            ["BCC"] = 0.30,
            ["BCD"] = 0.30,
            ["BCE"] = 0.30
        };

        return new AiPlatformSettings(
            Enabled: source.Enabled ?? AiSettingsFactory.ParseBool(Env("SEWERSTUDIO_AI_ENABLED")),
            OllamaBaseUri: new Uri(FirstNonEmpty(source.OllamaUrl, Env("SEWERSTUDIO_OLLAMA_URL")) ?? "http://localhost:11434"),
            VisionModel: vision,
            TextModel: FirstNonEmpty(source.TextModel, Env("SEWERSTUDIO_AI_TEXT_MODEL")) ?? OllamaConfig.DefaultTextModel,
            EmbedModel: FirstNonEmpty(source.EmbedModel, Env("SEWERSTUDIO_AI_EMBED_MODEL")) ?? OllamaConfig.DefaultEmbedModel,
            OllamaRequestTimeout: TimeSpan.FromMinutes(source.OllamaTimeoutMin ?? AiSettingsFactory.ParseInt(Env("SEWERSTUDIO_AI_TIMEOUT_MIN")) ?? 5),
            OllamaKeepAlive: FirstNonEmpty(source.OllamaKeepAlive, Env("SEWERSTUDIO_OLLAMA_KEEP_ALIVE")) ?? OllamaConfig.DefaultKeepAlive,
            OllamaNumCtx: source.OllamaNumCtx ?? AiSettingsFactory.ParseInt(Env("SEWERSTUDIO_OLLAMA_NUM_CTX")) ?? numCtxDefault,
            MultiModelEnabled: source.MultiModelEnabled ?? AiSettingsFactory.ParseBool(Env("SEWERSTUDIO_MULTIMODEL_ENABLED")),
            SidecarUrl: new Uri(FirstNonEmpty(source.SidecarUrl, Env("SEWERSTUDIO_SIDECAR_URL")) ?? "http://localhost:8100"),
            SidecarToken: FirstNonEmpty(
                source.SidecarToken,
                Env("SEWERSTUDIO_SIDECAR_TOKEN"),
                RawEnv("SEWER_SIDECAR_AUTH_TOKEN"),
                RawEnv("SEWER_SIDECAR_TOKEN")),
            PipelineMode: AiSettingsFactory.ParsePipelineMode(FirstNonEmpty(source.PipelineMode, Env("SEWERSTUDIO_PIPELINE_MODE"))),
            YoloConfidence: source.YoloConfidence ?? AiSettingsFactory.ParseDouble(Env("SEWERSTUDIO_YOLO_CONFIDENCE")) ?? 0.25,
            YoloClassConfidence: yoloClassConf,
            // Seit dem A/B-Lauf 2026-06-10 bewusst 0.25/0.20, damit
            // Befund-Frames ohne erste DINO-Box nicht vorschnell entfallen.
            DinoBoxThreshold: source.DinoBoxThreshold ?? AiSettingsFactory.ParseDouble(Env("SEWERSTUDIO_DINO_BOX_THRESHOLD")) ?? 0.25,
            DinoTextThreshold: source.DinoTextThreshold ?? AiSettingsFactory.ParseDouble(Env("SEWERSTUDIO_DINO_TEXT_THRESHOLD")) ?? 0.20,
            SidecarTimeoutSec: AiSettingsFactory.ParseInt(Env("SEWERSTUDIO_SIDECAR_TIMEOUT_SEC")) ?? 300,
            PipeDiameterMmOverride: source.PipeDiameterMm ?? AiSettingsFactory.ParseInt(Env("SEWERSTUDIO_PIPE_DIAMETER_MM")),
            FfmpegPath: FirstNonEmpty(source.FfmpegPath, Env("SEWERSTUDIO_FFMPEG")) ?? "ffmpeg");
    }

    private string? Env(string name)
    {
        var value = _getEnvironmentVariable(name)?.Trim();
        if (!string.IsNullOrEmpty(value))
            return value;

        if (name.StartsWith("SEWERSTUDIO_", StringComparison.Ordinal))
        {
            return _getEnvironmentVariable(
                "AUSWERTUNGPRO_" + name["SEWERSTUDIO_".Length..])?.Trim();
        }

        return null;
    }

    private string? RawEnv(string name)
    {
        var value = _getEnvironmentVariable(name)?.Trim();
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

/// <summary>Kompatibilitaetsfassade; reine Parser bleiben statisch.</summary>
public static class AiSettingsFactory
{
    private static readonly IAiPlatformSettingsResolver Default =
        new AiPlatformSettingsResolver(GpuModelSelector.Current);

    public static IAiPlatformSettingsResolver Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IAiPlatformSettingsResolver resolver) =>
        throw new NotSupportedException(
            "Der globale KI-Einstellungsdienst kann nicht mehr ausgetauscht werden. " +
            "IAiPlatformSettingsResolver bitte per Konstruktor uebergeben.");

    public static AiPlatformSettings Load(AiSettingsSource? source = null) =>
        Current.Load(source);

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
}
