using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingStreckenschadenObservationBuildResult(
    HashSet<SegmentedFinding> ConsumedSegments,
    IReadOnlyList<StreckenschadenTracker.Observation> Observations);

public static class CodingStreckenschadenObservationBuilder
{
    public static CodingStreckenschadenObservationBuildResult Build(
        IReadOnlyList<SegmentedFinding> segmented,
        double meter,
        Func<LiveFrameFinding, double, string?> resolveCode,
        Func<string, bool>? isStretchCode = null)
    {
        var consumed = new HashSet<SegmentedFinding>();
        var observations = new List<StreckenschadenTracker.Observation>();
        var stretchPredicate = isStretchCode ?? VsaCodeResolver.IsStreckenschadenCode;

        foreach (var seg in segmented)
        {
            if (!seg.Proximity.IsCodierbar)
                continue;

            var q = seg.Quant;
            var pseudoFinding = new LiveFrameFinding(
                Label: q.Label,
                Severity: QuantificationSeverityPolicy.Estimate(
                    q.CrossSectionReductionPercent,
                    q.IntrusionPercent,
                    q.HeightMm,
                    q.ExtentPercent),
                PositionClock: VsaCodeResolver.NormalizeClock(q.ClockPosition),
                ExtentPercent: q.ExtentPercent,
                VsaCodeHint: null);

            var code = resolveCode(pseudoFinding, meter);
            if (code == null || !stretchPredicate(code))
                continue;

            consumed.Add(seg);
            observations.Add(new StreckenschadenTracker.Observation(
                MainCode: code,
                ClockHour: LiveDetectionGeometryMapper.ParseClockHour(q.ClockPosition),
                Meter: meter));
        }

        return new CodingStreckenschadenObservationBuildResult(consumed, observations);
    }
}
