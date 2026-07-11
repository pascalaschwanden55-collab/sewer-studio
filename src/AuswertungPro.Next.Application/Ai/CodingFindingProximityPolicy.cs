using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

public static class CodingFindingProximityPolicy
{
    public static bool IsTooFarAhead(
        LiveFrameFinding finding,
        PipeCalibration? calibration,
        double videoAspect)
    {
        if (!(finding.BboxX1.HasValue && finding.BboxY1.HasValue
              && finding.BboxX2.HasValue && finding.BboxY2.HasValue))
        {
            return false;
        }

        var vanishX = calibration?.PipeCenter.X ?? 0.5;
        var vanishY = calibration?.PipeCenter.Y ?? 0.5;
        var pipeRadius = calibration is { NormalizedDiameter: > 0 }
            ? calibration.NormalizedDiameter / 2.0
            : 0.5;
        var aspect = videoAspect > 0 ? videoAspect : 1.0;

        var input = new MetrierungProximityInput(
            Math.Min(finding.BboxX1.Value, finding.BboxX2.Value),
            Math.Min(finding.BboxY1.Value, finding.BboxY2.Value),
            Math.Max(finding.BboxX1.Value, finding.BboxX2.Value),
            Math.Max(finding.BboxY1.Value, finding.BboxY2.Value),
            vanishX,
            vanishY,
            aspect,
            pipeRadius);

        var result = MetrierungProximityEvaluator.Evaluate(input, MetrierungProximityThresholds.Default);
        return !result.IsCodierbar;
    }
}
