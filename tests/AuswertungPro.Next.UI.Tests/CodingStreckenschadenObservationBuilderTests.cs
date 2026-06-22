using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStreckenschadenObservationBuilderTests
{
    [Fact]
    public void Build_consumes_only_codierbar_resolved_stretch_damage_segments()
    {
        var stretch = Finding("long crack", MetrierungProximity.Codierbar, clock: "10 Uhr", extentPercent: 60);
        var ahead = Finding("ahead crack", MetrierungProximity.Voraus, clock: "3 Uhr");
        var nonStretch = Finding("point crack", MetrierungProximity.Codierbar, clock: "6 Uhr");
        var unresolved = Finding("unknown", MetrierungProximity.Codierbar, clock: "9 Uhr");
        var resolvedFindings = new List<LiveFrameFinding>();

        var result = CodingStreckenschadenObservationBuilder.Build(
            [stretch, ahead, nonStretch, unresolved],
            meter: 12.5,
            resolveCode: (finding, _) =>
            {
                resolvedFindings.Add(finding);
                return finding.Label switch
                {
                    "long crack" => "BBA",
                    "point crack" => "BCA",
                    _ => null
                };
            },
            isStretchCode: code => string.Equals(code, "BBA", StringComparison.OrdinalIgnoreCase));

        var observation = Assert.Single(result.Observations);
        Assert.Equal("BBA", observation.MainCode);
        Assert.Equal(10, observation.ClockHour);
        Assert.Equal(12.5, observation.Meter);
        Assert.Contains(stretch, result.ConsumedSegments);
        Assert.DoesNotContain(ahead, result.ConsumedSegments);
        Assert.DoesNotContain(nonStretch, result.ConsumedSegments);
        Assert.DoesNotContain(unresolved, result.ConsumedSegments);
        Assert.Equal(3, resolvedFindings.Count);
        Assert.Equal(4, resolvedFindings[0].Severity);
        Assert.Equal("10:00", resolvedFindings[0].PositionClock);
    }

    [Fact]
    public void Build_returns_empty_result_without_coding_segments()
    {
        var result = CodingStreckenschadenObservationBuilder.Build(
            [Finding("ahead crack", MetrierungProximity.Voraus)],
            meter: 1.0,
            resolveCode: (_, _) => "BBA",
            isStretchCode: _ => true);

        Assert.Empty(result.Observations);
        Assert.Empty(result.ConsumedSegments);
    }

    private static SegmentedFinding Finding(
        string label,
        MetrierungProximity proximity,
        string? clock = null,
        int? extentPercent = null)
    {
        var mask = new SamMaskResult(
            Label: label,
            Confidence: 0.9,
            Bbox: [0, 0, 100, 100],
            MaskRle: "0",
            MaskAreaPixels: 100,
            ImageAreaPixels: 10_000,
            HeightPixels: 100,
            WidthPixels: 100,
            CentroidX: 50,
            CentroidY: 50);
        var quant = new MaskQuantificationService.QuantifiedMask(
            label,
            0.9,
            null,
            null,
            extentPercent,
            null,
            null,
            clock);
        var proximityResult = new MetrierungProximityResult(proximity, "", 0, 0, 0, false, false);
        return new SegmentedFinding(null, mask, quant, proximityResult);
    }
}
