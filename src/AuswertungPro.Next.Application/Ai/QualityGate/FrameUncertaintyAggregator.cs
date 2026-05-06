using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.QualityGate;

/// <summary>
/// V4.2 Phase 1.2: Aggregiert per-Detection-Unsicherheiten zu einem Frame-Level-Score.
/// Genutzt vom UncertaintySamplingService (Phase 1.4) fuer Review-Priorisierung:
/// je hoeher der Frame-Score, desto frueher sollte ein Mensch den Frame pruefen.
///
/// Reuse: <see cref="UncertaintyEstimate.FromSinglePass"/> liefert die Per-Detection-Werte.
/// </summary>
public static class FrameUncertaintyAggregator
{
    /// <summary>ViewType-Werte, die Schaeden zuverlaessig erkennbar machen.</summary>
    private static readonly HashSet<string> CodableViewTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "axial", "schacht"
    };

    /// <summary>
    /// Aggregiert die Detections eines Frames zu einem Frame-Uncertainty-Score.
    /// </summary>
    public static FrameUncertaintyScore Aggregate(
        IReadOnlyList<BlindDetection> detections,
        string? viewType = null,
        double? viewTypeConfidence = null)
    {
        if (detections.Count == 0)
        {
            return new FrameUncertaintyScore(
                Epistemic: 0.0,
                Aleatoric: 0.0,
                MinConfidence: 1.0,
                ViewTypeConflictBoost: 0.0,
                DetectionCount: 0,
                Source: "empty");
        }

        var estimates = detections
            .Select(d => UncertaintyEstimate.FromSinglePass(Math.Clamp(d.Confidence, 0.0, 1.0)))
            .ToList();

        var maxEpistemic = estimates.Max(e => e.EpistemicUncertainty);
        var meanAleatoric = estimates.Average(e => e.AleatoricUncertainty);
        var minConf = detections.Min(d => Math.Clamp(d.Confidence, 0.0, 1.0));

        var viewTypeBoost = 0.0;
        if (!string.IsNullOrWhiteSpace(viewType) &&
            !CodableViewTypes.Contains(viewType) &&
            detections.Any(d => !string.IsNullOrEmpty(d.VsaCode)))
        {
            var conf = viewTypeConfidence ?? 0.5;
            viewTypeBoost = 0.5 * Math.Clamp(conf, 0.0, 1.0);
        }

        return new FrameUncertaintyScore(
            Epistemic: maxEpistemic,
            Aleatoric: meanAleatoric,
            MinConfidence: minConf,
            ViewTypeConflictBoost: viewTypeBoost,
            DetectionCount: detections.Count,
            Source: "single-pass");
    }
}

/// <summary>
/// Aggregierter Frame-Level-Uncertainty-Score aus <see cref="FrameUncertaintyAggregator"/>.
/// </summary>
public sealed record FrameUncertaintyScore(
    double Epistemic,
    double Aleatoric,
    double MinConfidence,
    double ViewTypeConflictBoost,
    int DetectionCount,
    string Source)
{
    /// <summary>Gesamt-Unsicherheit (Epistemic + Aleatoric + ViewType-Boost), clamped auf [0, 1].</summary>
    public double Total => Math.Clamp(Epistemic + Aleatoric + ViewTypeConflictBoost, 0.0, 1.0);

    /// <summary>True wenn Frame manuell gepruefet werden sollte.</summary>
    public bool NeedsReview => Epistemic >= 0.15
        || (MinConfidence >= 0.45 && MinConfidence < 0.75)
        || ViewTypeConflictBoost > 0.0;
}
