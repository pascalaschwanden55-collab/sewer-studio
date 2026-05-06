using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Pipeline;

namespace AuswertungPro.Next.Pipeline.Tests;

public class DetectionAggregatorTests
{
    private static FrameDetection MakeDetection(int classId, string className,
        double confidence, double time, double meter, string frame = "f.png")
        => new()
        {
            YoloClassId = classId, YoloClassName = className,
            Confidence = confidence, TimeSeconds = time,
            Meter = meter, FramePath = frame
        };

    [Fact]
    public void SingleDetection_BelowMinFrames_NoEvent()
    {
        var agg = new DetectionAggregator();
        agg.Feed(MakeDetection(0, "crack", 0.8, 1.0, 5.0));

        var events = agg.Flush();

        Assert.Empty(events);
    }

    [Fact]
    public void ThreeConsecutiveFrames_SameClass_ProducesEvent()
    {
        var agg = new DetectionAggregator();
        agg.Feed(MakeDetection(0, "crack", 0.6, 1.0, 5.0, "f1.png"));
        agg.Feed(MakeDetection(0, "crack", 0.9, 2.0, 5.5, "f2.png"));
        agg.Feed(MakeDetection(0, "crack", 0.7, 3.0, 6.0, "f3.png"));

        var events = agg.Flush();

        Assert.Single(events);
        var e = events[0];
        Assert.Equal(0, e.YoloClassId);
        Assert.Equal("crack", e.YoloClassName);
        Assert.Equal(0.9, e.PeakConfidence);
        Assert.Equal("f2.png", e.PeakFramePath);
        Assert.Equal(2.0, e.PeakTimeSeconds);
        Assert.Equal(3, e.FrameCount);
    }

    [Fact]
    public void TwoDifferentClasses_ProducesTwoEvents()
    {
        var agg = new DetectionAggregator();
        for (int i = 0; i < 3; i++)
            agg.Feed(MakeDetection(0, "crack", 0.8, i, 5.0 + i * 0.1));
        for (int i = 0; i < 3; i++)
            agg.Feed(MakeDetection(1, "root", 0.7, 3 + i, 5.0 + i * 0.1));

        var events = agg.Flush();

        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.YoloClassName == "crack");
        Assert.Contains(events, e => e.YoloClassName == "root");
    }

    [Fact]
    public void SameClass_DifferentMeterLocations_ProducesTwoEvents()
    {
        var agg = new DetectionAggregator();
        // Riss bei Meter 5
        for (int i = 0; i < 3; i++)
            agg.Feed(MakeDetection(0, "crack", 0.8, i, 5.0 + i * 0.1));
        // Riss bei Meter 20 — weit ausserhalb meterMergeRadius
        for (int i = 0; i < 3; i++)
            agg.Feed(MakeDetection(0, "crack", 0.8, 3 + i, 20.0 + i * 0.1));

        var events = agg.Flush();

        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void LowConfidence_BelowThreshold_Ignored()
    {
        var agg = new DetectionAggregator();
        for (int i = 0; i < 5; i++)
            agg.Feed(MakeDetection(0, "crack", 0.3, i, 5.0));

        var events = agg.Flush();

        Assert.Empty(events);
    }

    [Fact]
    public void GapInFrames_ClosesEvent()
    {
        // maxGapFrames=5 standardmaessig
        var agg = new DetectionAggregator();
        var allFeedEvents = new List<DetectionEvent>();

        // Erste Gruppe: 3 Frames (Riss bei Meter 5)
        for (int i = 0; i < 3; i++)
        {
            var r = agg.Feed(MakeDetection(0, "crack", 0.8, i, 5.0 + i * 0.1));
            if (r != null) allFeedEvents.Add(r);
        }

        // Luecke: 6 Feed-Aufrufe ohne crack — erzwinge Gap-Schliessung
        for (int i = 0; i < 6; i++)
        {
            var r = agg.Feed(MakeDetection(1, "deposit", 0.1, 3 + i, 10.0));
            if (r != null) allFeedEvents.Add(r);
        }

        // Zweite Gruppe: 3 Frames (Riss bei Meter 25)
        for (int i = 0; i < 3; i++)
        {
            var r = agg.Feed(MakeDetection(0, "crack", 0.8, 9 + i, 25.0 + i * 0.1));
            if (r != null) allFeedEvents.Add(r);
        }

        var flushed = agg.Flush();
        allFeedEvents.AddRange(flushed);

        // Nur crack-Events zaehlen
        var crackEvents = allFeedEvents.Where(e => e.YoloClassName == "crack").ToList();
        Assert.Equal(2, crackEvents.Count);
    }

    [Fact]
    public void Streckenschaden_MeterStartDiffersFromEnd()
    {
        var agg = new DetectionAggregator();
        // Ablagerung ueber 3 Meter
        agg.Feed(MakeDetection(2, "deposit", 0.8, 1.0, 10.0));
        agg.Feed(MakeDetection(2, "deposit", 0.9, 2.0, 11.5));
        agg.Feed(MakeDetection(2, "deposit", 0.7, 3.0, 13.0));

        var events = agg.Flush();

        Assert.Single(events);
        var e = events[0];
        Assert.Equal(10.0, e.MeterStart);
        Assert.Equal(13.0, e.MeterEnd);
        Assert.NotEqual(e.MeterStart, e.MeterEnd);
    }
}
