using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Uebertraegt das technische Multi-Model-Ergebnis in das gemeinsame Analyseformat.
/// Die Ablaufsteuerung bleibt dadurch von Quantifizierung und Feldzuordnung getrennt.
/// </summary>
internal static class MultiModelFrameAnalysisMapper
{
    internal static EnhancedFrameAnalysis Map(
        MultiModelFrameResult result,
        int pipeDiameterMm)
    {
        if (!result.IsRelevant)
            return EnhancedFrameAnalysis.Empty();

        var quantified = new List<MaskQuantificationService.QuantifiedMask>();
        foreach (var mask in result.SamMasks)
        {
            quantified.Add(MaskQuantificationService.Quantify(
                mask, result.ImageWidth, result.ImageHeight, pipeDiameterMm));
        }

        var findings = new List<EnhancedFinding>(quantified.Count);
        for (var i = 0; i < quantified.Count; i++)
        {
            var quantifiedMask = quantified[i];
            if (string.IsNullOrWhiteSpace(quantifiedMask.Label))
                continue;

            var bbox = GetNormalizedBbox(result.SamMasks[i], result.ImageWidth, result.ImageHeight);
            findings.Add(new EnhancedFinding(
                Label: quantifiedMask.Label,
                VsaCodeHint: VsaCodeResolver.InferCodeFromLabel(quantifiedMask.Label),
                Severity: QuantificationSeverityPolicy.Estimate(
                    quantifiedMask.CrossSectionReductionPercent,
                    quantifiedMask.IntrusionPercent,
                    quantifiedMask.HeightMm,
                    quantifiedMask.ExtentPercent),
                PositionClock: VsaCodeResolver.NormalizeClock(quantifiedMask.ClockPosition),
                ExtentPercent: quantifiedMask.ExtentPercent,
                HeightMm: quantifiedMask.HeightMm,
                WidthMm: quantifiedMask.WidthMm,
                IntrusionPercent: quantifiedMask.IntrusionPercent,
                CrossSectionReductionPercent: quantifiedMask.CrossSectionReductionPercent,
                DiameterReductionMm: null,
                BboxX1: bbox.X1,
                BboxY1: bbox.Y1,
                BboxX2: bbox.X2,
                BboxY2: bbox.Y2,
                Notes: null));
        }

        return new EnhancedFrameAnalysis(
            Meter: result.Meter,
            PipeMaterial: "unbekannt",
            PipeDiameterMm: pipeDiameterMm,
            Findings: findings,
            ImageQuality: "gut",
            IsEmptyFrame: false,
            Error: null,
            Outcome: findings.Count == 0
                ? AnalysisOutcome.NoFinding
                : AnalysisOutcome.Ok);
    }

    internal static (double? X1, double? Y1, double? X2, double? Y2) GetNormalizedBbox(
        SamMaskResult mask,
        int imageWidth,
        int imageHeight)
    {
        if (mask.Bbox == null || mask.Bbox.Count < 4 || imageWidth <= 0 || imageHeight <= 0)
            return default;

        return (
            Clamp01(mask.Bbox[0] / imageWidth),
            Clamp01(mask.Bbox[1] / imageHeight),
            Clamp01(mask.Bbox[2] / imageWidth),
            Clamp01(mask.Bbox[3] / imageHeight));
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
}
