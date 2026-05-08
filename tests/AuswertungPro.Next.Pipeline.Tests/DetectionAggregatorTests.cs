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

    // =====================================================================
    // Phase 3.3 — Zusatztests fuer DetectionAggregator (Test-Roadmap p1.x)
    // =====================================================================

    [Fact]
    public void Gap_Schliessen_Erzeugt_Zwei_Getrennte_Detections_Bei_Gleicher_Klasse()
    {
        // Szenario: Class X bei Frame 1-3, dann 6 Feeds Pause (>maxGapFrames=5),
        // dann erneut Class X bei deutlich anderem Meterstand.
        // Erwartung: ZWEI getrennte Events fuer dieselbe Klasse.
        var agg = new DetectionAggregator();
        var collected = new List<DetectionEvent>();

        // Erste Detection-Phase (Meter 5)
        for (int i = 0; i < 3; i++)
        {
            var ev = agg.Feed(MakeDetection(0, "crack", 0.8, i, 5.0 + i * 0.05, $"a{i}.png"));
            if (ev != null) collected.Add(ev);
        }

        // Luecke: 6 Feeds einer anderen Klasse mit zu niedriger Confidence
        // (zaehlen nicht als aktive Detections, treiben aber _feedIndex hoch)
        for (int i = 0; i < 6; i++)
        {
            var ev = agg.Feed(MakeDetection(9, "noise", 0.05, 3 + i, 5.5));
            if (ev != null) collected.Add(ev);
        }

        // Zweite Detection-Phase (Meter 30 — weit ausserhalb meterMergeRadius)
        for (int i = 0; i < 3; i++)
        {
            var ev = agg.Feed(MakeDetection(0, "crack", 0.8, 9 + i, 30.0 + i * 0.05, $"b{i}.png"));
            if (ev != null) collected.Add(ev);
        }

        collected.AddRange(agg.Flush());

        var crackEvents = collected.Where(e => e.YoloClassName == "crack").ToList();
        Assert.Equal(2, crackEvents.Count);
        Assert.Contains(crackEvents, e => Math.Abs(e.MeterStart - 5.0) < 0.5);
        Assert.Contains(crackEvents, e => Math.Abs(e.MeterStart - 30.0) < 0.5);
    }

    [Fact]
    public void Meter_Fusion_Innerhalb_Toleranz_Erzeugt_Eine_Detection()
    {
        // Default meterMergeRadius=1.5 — Detects bei 12.5 und 12.8 (Abstand 0.3) sollten fusionieren.
        // Drei Frames noetig, weil minConsecutiveFrames=3.
        var agg = new DetectionAggregator();

        agg.Feed(MakeDetection(0, "crack", 0.7, 1.0, 12.5, "f1.png"));
        agg.Feed(MakeDetection(0, "crack", 0.9, 2.0, 12.8, "f2.png"));
        agg.Feed(MakeDetection(0, "crack", 0.6, 3.0, 12.6, "f3.png"));

        var events = agg.Flush();

        Assert.Single(events);
        var e = events[0];
        Assert.Equal("crack", e.YoloClassName);
        Assert.Equal(3, e.FrameCount);
        // MeterStart = kleinster, MeterEnd = groesster
        Assert.Equal(12.5, e.MeterStart);
        Assert.Equal(12.8, e.MeterEnd);
        // Peak-Konfidenz vom mittleren Frame
        Assert.Equal(0.9, e.PeakConfidence);
        Assert.Equal("f2.png", e.PeakFramePath);
    }

    [Fact]
    public void Verschiedene_Klassen_Werden_Parallel_Unabhaengig_Getrackt()
    {
        // Zwei Klassen gleichzeitig (interleaved Feeds) — sollten unabhaengig zu
        // je einem Event aggregiert werden.
        var agg = new DetectionAggregator();

        // Interleaved: crack und root abwechselnd, jeweils 3 Frames
        agg.Feed(MakeDetection(0, "crack", 0.8, 1.0, 5.0, "c1.png"));
        agg.Feed(MakeDetection(1, "root", 0.7, 1.1, 5.0, "r1.png"));
        agg.Feed(MakeDetection(0, "crack", 0.85, 2.0, 5.2, "c2.png"));
        agg.Feed(MakeDetection(1, "root", 0.75, 2.1, 5.2, "r2.png"));
        agg.Feed(MakeDetection(0, "crack", 0.9, 3.0, 5.4, "c3.png"));
        agg.Feed(MakeDetection(1, "root", 0.8, 3.1, 5.4, "r3.png"));

        var events = agg.Flush();

        Assert.Equal(2, events.Count);
        var crack = events.Single(e => e.YoloClassId == 0);
        var root = events.Single(e => e.YoloClassId == 1);

        Assert.Equal("crack", crack.YoloClassName);
        Assert.Equal(3, crack.FrameCount);
        Assert.Equal(0.9, crack.PeakConfidence);

        Assert.Equal("root", root.YoloClassName);
        Assert.Equal(3, root.FrameCount);
        Assert.Equal(0.8, root.PeakConfidence);
    }

    [Fact]
    public void BCD_Und_BCE_Grundgeruest_Codes_Werden_Wie_Standard_Klassen_Aggregiert()
    {
        // Charakterisierung: aktuelles Verhalten — der DetectionAggregator hat
        // KEINE Sonderlogik fuer BCD (Rohranfang) oder BCE (Rohrende). Die
        // Aggregation laeuft rein ueber YoloClassId + Meter-Toleranz wie bei
        // jedem anderen Schadenscode. Dieser Test fixiert dieses Verhalten
        // damit es nicht stillschweigend kippt.
        //
        // Wichtig: Feed() liefert geschlossene Events bei Gap-Ueberschreitung
        // (BCD wird beim ersten BCE-Feed geschlossen, weil _maxGapFrames=5
        // ueberschritten ist). Wer den BCD-Event will, MUSS den Feed-Return
        // sammeln. Flush() liefert nur die noch aktiven Events.
        var agg = new DetectionAggregator();
        var collected = new List<DetectionEvent>();

        void FeedAndCollect(FrameDetection d)
        {
            var ev = agg.Feed(d);
            if (ev != null) collected.Add(ev);
        }

        // BCD am Rohranfang (Meter ~0)
        FeedAndCollect(MakeDetection(7, "BCD", 0.95, 0.0, 0.0, "bcd1.png"));
        FeedAndCollect(MakeDetection(7, "BCD", 0.96, 0.2, 0.1, "bcd2.png"));
        FeedAndCollect(MakeDetection(7, "BCD", 0.94, 0.4, 0.2, "bcd3.png"));

        // Mittelstueck — andere Klasse unter Confidence-Schwelle, treibt Gap hoch
        for (int i = 0; i < 8; i++)
            FeedAndCollect(MakeDetection(0, "crack", 0.05, 5 + i, 10.0 + i));

        // BCE am Rohrende (Meter ~50) — schliesst BCD durch Gap
        FeedAndCollect(MakeDetection(8, "BCE", 0.93, 50.0, 50.0, "bce1.png"));
        FeedAndCollect(MakeDetection(8, "BCE", 0.94, 50.2, 50.1, "bce2.png"));
        FeedAndCollect(MakeDetection(8, "BCE", 0.95, 50.4, 50.2, "bce3.png"));

        // Restliche aktive (BCE) noch flushen
        collected.AddRange(agg.Flush());

        // Erwartung: zwei getrennte Events — eins fuer BCD, eins fuer BCE
        var bcd = collected.SingleOrDefault(e => e.YoloClassName == "BCD");
        var bce = collected.SingleOrDefault(e => e.YoloClassName == "BCE");

        Assert.NotNull(bcd);
        Assert.NotNull(bce);
        Assert.Equal(3, bcd!.FrameCount);
        Assert.Equal(3, bce!.FrameCount);
        Assert.True(bcd.MeterStart < bce.MeterStart, "BCD (Rohranfang) muss vor BCE (Rohrende) liegen");
    }

    [Fact]
    public void Label_Drift_Bei_Verschiedener_Schreibweise_Wird_Aktuell_Nicht_Fusioniert()
    {
        // Charakterisierung: aktuelles Verhalten — der Aggregator gruppiert
        // ueber YoloClassId, NICHT ueber YoloClassName. Selbst wenn der Label-
        // String "crack" / "Crack" / " crack " variiert: solange die ClassId
        // identisch ist, werden sie als gleiche Klasse zusammengefuehrt.
        // Dieser Test fixiert die Vereinbarung "ClassId ist die Wahrheit,
        // Name ist nur Anzeige".
        var agg = new DetectionAggregator();

        // Drei Frames mit identischer ClassId aber leicht abweichendem Label
        agg.Feed(MakeDetection(0, "crack", 0.7, 1.0, 5.0, "f1.png"));
        agg.Feed(MakeDetection(0, "Crack", 0.85, 2.0, 5.2, "f2.png")); // case-Drift
        agg.Feed(MakeDetection(0, " crack ", 0.75, 3.0, 5.4, "f3.png")); // whitespace-Drift

        var events = agg.Flush();

        // Aktuelles Verhalten: alle drei werden als EIN Event aggregiert,
        // weil der Match ueber YoloClassId laeuft. Der ClassName des
        // Resultats ist der des ERSTEN Frames (kein Re-Naming).
        Assert.Single(events);
        var e = events[0];
        Assert.Equal(0, e.YoloClassId);
        Assert.Equal(3, e.FrameCount);
        Assert.Equal("crack", e.YoloClassName); // erster Frame setzt den Namen
        Assert.Equal(0.85, e.PeakConfidence);
    }
}
