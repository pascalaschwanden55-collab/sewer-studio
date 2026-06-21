using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Infrastructure.Tests;

public class TemporalFindingDeduplicatorTests
{
    [Fact]
    public void Update_SameStretchFindingWithinWindow_MergesMetersAndSeverity()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 3
        });

        Assert.Empty(deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3:00") }, 5.0));
        Assert.Empty(deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 4, "3") }, 6.1));

        var detection = Assert.Single(deduplicator.Flush());
        Assert.Equal(5.0, detection.MeterStart);
        Assert.Equal(6.1, detection.MeterEnd);
        Assert.Equal("high", detection.Severity);
        Assert.Equal("BBA", detection.VsaCodeHint);
    }

    [Fact]
    public void AdvanceAll_ClosesFindingAfterDedupWindow()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 2
        });

        Assert.Empty(deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 5.0));
        Assert.Empty(deduplicator.AdvanceAll());

        var detection = Assert.Single(deduplicator.AdvanceAll());
        Assert.Equal(5.0, detection.MeterStart);
        Assert.Equal(5.0, detection.MeterEnd);
    }

    [Fact]
    public void Update_SameCodeWithDifferentClock_KeepsSeparateFindings()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 3
        });

        deduplicator.Update(new[]
        {
            Finding("Riss", "BAB", 2, "3:00"),
            Finding("Riss", "BAB", 2, "9:00")
        }, 4.0);

        var detections = deduplicator.Flush();
        Assert.Equal(2, detections.Count);
        Assert.Contains(detections, d => d.PositionClock == "3:00");
        Assert.Contains(detections, d => d.PositionClock == "9:00");
    }

    [Fact]
    public void Update_MergesEvidenceByKeepingStrongestSignalsAndFrameCount()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 3
        });

        var firstEvidence = new EvidenceVector(YoloConf: 0.42, DinoConf: 0.7, FrameCount: 1);
        var secondEvidence = new EvidenceVector(YoloConf: 0.91, SamMaskStability: 0.8, FrameCount: 1);

        deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 5.0, firstEvidence);
        deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 5.4, secondEvidence);

        var detection = Assert.Single(deduplicator.Flush());
        Assert.NotNull(detection.Evidence);
        Assert.Equal(0.91, detection.Evidence.YoloConf);
        Assert.Equal(0.7, detection.Evidence.DinoConf);
        Assert.Equal(0.8, detection.Evidence.SamMaskStability);
        Assert.Equal(2, detection.Evidence.FrameCount);
    }

    [Fact]
    public void Update_GapGreaterThanOneMeter_StartsNewFinding()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 3,
            MeterMergeGapMaxMeters = 1.0
        });

        Assert.Empty(deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 5.0));
        Assert.Empty(deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 6.0));

        var completed = deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 7.1);

        var first = Assert.Single(completed);
        Assert.Equal(5.0, first.MeterStart);
        Assert.Equal(6.0, first.MeterEnd);

        var second = Assert.Single(deduplicator.Flush());
        Assert.Equal(7.1, second.MeterStart);
        Assert.Equal(7.1, second.MeterEnd);
    }

    [Fact]
    public void Update_GapAtOneMeter_KeepsSameFinding()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 3,
            MeterMergeGapMaxMeters = 1.0
        });

        deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 5.0);
        Assert.Empty(deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 6.0));

        var detection = Assert.Single(deduplicator.Flush());
        Assert.Equal(5.0, detection.MeterStart);
        Assert.Equal(6.0, detection.MeterEnd);
    }

    [Fact]
    public void Update_ReverseMeterDirection_KeepsStretchRangeOrdered()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 3
        });

        deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 10.0);
        deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 8.7);

        var detection = Assert.Single(deduplicator.Flush());
        Assert.Equal(8.7, detection.MeterStart);
        Assert.Equal(10.0, detection.MeterEnd);
    }

    [Fact]
    public void Update_ReverseMeterGapGreaterThanMax_StartsNewFinding()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 3,
            MeterMergeGapMaxMeters = 1.0
        });

        Assert.Empty(deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 10.0));
        Assert.Empty(deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 9.0));

        var completed = deduplicator.Update(new[] { Finding("Wurzeln", "BBA", 2, "3") }, 7.8);

        var first = Assert.Single(completed);
        Assert.Equal(9.0, first.MeterStart);
        Assert.Equal(10.0, first.MeterEnd);

        var second = Assert.Single(deduplicator.Flush());
        Assert.Equal(7.8, second.MeterStart);
        Assert.Equal(7.8, second.MeterEnd);
    }

    private static EnhancedFinding Finding(string label, string? code, int severity, string? clock) =>
        new(
            Label: label,
            VsaCodeHint: code,
            Severity: severity,
            PositionClock: clock,
            ExtentPercent: null,
            HeightMm: null,
            WidthMm: null,
            IntrusionPercent: null,
            CrossSectionReductionPercent: null,
            DiameterReductionMm: null,
            BboxX1: null,
            BboxY1: null,
            BboxX2: null,
            BboxY2: null,
            Notes: null);
}
