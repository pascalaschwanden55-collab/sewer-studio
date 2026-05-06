using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Configuration for the Multi-Model Vision Pipeline Sidecar.
/// Values are loaded via unified AiPlatformConfig.
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
    bool McDropoutEnabled = true,
    /// <summary>Rohrmaterial fuer Plausibilitaetsfilter (z.B. "Polyethylen", "Beton", "Steinzeug").</summary>
    string? PipeMaterial = null,
    /// <summary>DetectionAggregator: Mindestanzahl aufeinanderfolgender Frames fuer ein Schadensereignis.</summary>
    int AggregatorMinFrames = 3,
    /// <summary>DetectionAggregator: Minimale Confidence fuer Aggregation.</summary>
    double AggregatorMinConfidence = 0.4,
    /// <summary>DetectionAggregator: Radius in Metern fuer Meter-basiertes Merging.</summary>
    double AggregatorMergeRadius = 1.5,
    /// <summary>DetectionAggregator: Maximale Luecke in Frames bevor ein Event geschlossen wird.</summary>
    int AggregatorMaxGap = 5
);

public enum PipelineMode
{
    /// <summary>Try Sidecar, fall back to Ollama if unavailable.</summary>
    Auto,
    /// <summary>Force Sidecar – error if not reachable.</summary>
    MultiModel,
    /// <summary>Ignore Sidecar, use Ollama only.</summary>
    OllamaOnly
}
