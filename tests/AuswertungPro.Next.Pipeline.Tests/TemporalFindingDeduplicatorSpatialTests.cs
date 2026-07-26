using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// F12: Schluessel-Kollision im selben Frame — gleichcodierte Befunde mit gleicher
/// Uhrlage duerfen nur verschmelzen, wenn sie raeumlich ueberlappen (BBox-IoU >=
/// Schwelle <see cref="TemporalDedupOptions.SameFrameMergeMinIoU"/>, Default 0.3).
/// Raeumlich getrennte Treffer sind eigenstaendige Schaeden; Befunde ohne BBox
/// verschmelzen weiterhin wie bisher (Bestandsschutz).
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class TemporalFindingDeduplicatorSpatialTests
{
    public TemporalFindingDeduplicatorSpatialTests()
    {
        VsaResolverTestCatalog.ConfigureDefault();
    }

    [Fact]
    public void Update_GleicherCodeUndClock_RaeumlichGetrennt_BleibenZweiBefunde()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 3
        });

        deduplicator.Update(new[]
        {
            Finding("BCC", extent: 40, bbox: (0.05, 0.05, 0.25, 0.25)),
            Finding("BCC", extent: 60, bbox: (0.70, 0.70, 0.95, 0.95))   // IoU = 0
        }, 5.0);

        var detections = deduplicator.Flush();
        Assert.Equal(2, detections.Count);
        Assert.All(detections, d => Assert.Equal("BCC", d.VsaCodeHint));
        Assert.Contains(detections, d => d.ExtentPercent == 40);
        Assert.Contains(detections, d => d.ExtentPercent == 60);
    }

    [Fact]
    public void Update_GleicherCodeUndClock_Ueberlappend_VerschmilztZuEinemBefund()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 3
        });

        deduplicator.Update(new[]
        {
            Finding("BCC", extent: 40, bbox: (0.10, 0.10, 0.50, 0.50)),
            // IoU ~ 0.62 gegenueber der ersten Box — deutlich ueber der 0.3-Schwelle.
            Finding("BCC", extent: 60, bbox: (0.15, 0.15, 0.55, 0.55))
        }, 5.0);

        var detection = Assert.Single(deduplicator.Flush());
        Assert.Equal("BCC", detection.VsaCodeHint);
        // Dominanter Befund (groesste Ausdehnung) traegt die Quantifizierung — wie bisher.
        Assert.Equal(60, detection.ExtentPercent);
    }

    [Fact]
    public void Update_GleicherCodeUndClock_OhneBBox_VerschmilztWieBisher()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 3
        });

        deduplicator.Update(new[]
        {
            Finding("BCC", extent: 40, bbox: null),
            Finding("BCC", extent: 60, bbox: null)
        }, 5.0);

        var detection = Assert.Single(deduplicator.Flush());
        Assert.Equal(60, detection.ExtentPercent);
    }

    [Fact]
    public void Update_DeaktivierteSchwelle_VerschmilztTrotzRaeumlicherTrennung()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 3,
            SameFrameMergeMinIoU = 0   // raeumliche Trennung explizit aus
        });

        deduplicator.Update(new[]
        {
            Finding("BCC", extent: 40, bbox: (0.05, 0.05, 0.25, 0.25)),
            Finding("BCC", extent: 60, bbox: (0.70, 0.70, 0.95, 0.95))
        }, 5.0);

        Assert.Single(deduplicator.Flush());
    }

    [Fact]
    public void Update_RaeumlichGetrennt_ZeitlicheFortschreibungBleibtUnveraendert()
    {
        var deduplicator = new TemporalFindingDeduplicator(new TemporalDedupOptions
        {
            DedupWindowFrames = 2
        });

        // Frame 1: zwei getrennte BCC. Frame 2+3: kein Befund mehr.
        Assert.Empty(deduplicator.Update(new[]
        {
            Finding("BCC", extent: 40, bbox: (0.05, 0.05, 0.25, 0.25)),
            Finding("BCC", extent: 60, bbox: (0.70, 0.70, 0.95, 0.95))
        }, 5.0));
        Assert.Empty(deduplicator.Update(System.Array.Empty<EnhancedFinding>(), 6.0));

        // Nach DedupWindowFrames Misses werden beide eigenstaendig abgeschlossen.
        var completed = deduplicator.Update(System.Array.Empty<EnhancedFinding>(), 7.0);
        Assert.Equal(2, completed.Count);
        Assert.Empty(deduplicator.Flush());
    }

    private static EnhancedFinding Finding(
        string code, int extent, (double X1, double Y1, double X2, double Y2)? bbox) =>
        new(
            Label: "Oberflaechenschaden",
            VsaCodeHint: code,
            Severity: 3,
            PositionClock: "3:00",
            ExtentPercent: extent,
            HeightMm: null,
            WidthMm: null,
            IntrusionPercent: null,
            CrossSectionReductionPercent: null,
            DiameterReductionMm: null,
            BboxX1: bbox?.X1,
            BboxY1: bbox?.Y1,
            BboxX2: bbox?.X2,
            BboxY2: bbox?.Y2,
            Notes: null);
}
