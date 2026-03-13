using System;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Configuration for the Multi-Model Vision Pipeline Sidecar.
/// Values are loaded via unified AiPlatformConfig.
/// </summary>
public sealed record PipelineConfig(
    bool MultiModelEnabled,
    Uri SidecarUrl,
    PipelineMode Mode,
    double YoloConfidence,
    double DinoBoxThreshold,
    double DinoTextThreshold,
    int SidecarTimeoutSec,
    int? PipeDiameterMmOverride,
    bool SamStabilityCheckEnabled = false,
    bool McDropoutEnabled = true
)
{
    /// <summary>Lädt via einheitliche AiPlatformConfig.</summary>
    public static PipelineConfig Load() =>
        AiPlatformConfig.Load().ToPipelineConfig();
}

public enum PipelineMode
{
    /// <summary>Try Sidecar, fall back to Ollama if unavailable.</summary>
    Auto,
    /// <summary>Force Sidecar – error if not reachable.</summary>
    MultiModel,
    /// <summary>Ignore Sidecar, use Ollama only.</summary>
    OllamaOnly
}
