using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Configuration for the multi-model vision sidecar.
/// Loading from AppSettings remains in the UI layer.
/// </summary>
public sealed record PipelineConfig(
    bool MultiModelEnabled,
    Uri SidecarUrl,
    PipelineMode Mode,
    double YoloConfidence,
    Dictionary<string, double> YoloClassConfidence,
    double DinoBoxThreshold,
    double DinoTextThreshold,
    int SidecarTimeoutSec,
    int? PipeDiameterMmOverride,
    bool SamStabilityCheckEnabled = false,
    bool McDropoutEnabled = true);

public enum PipelineMode
{
    /// <summary>Try sidecar, fall back to Ollama if unavailable.</summary>
    Auto,

    /// <summary>Force sidecar; error if not reachable.</summary>
    MultiModel,

    /// <summary>Ignore sidecar, use Ollama only.</summary>
    OllamaOnly
}
