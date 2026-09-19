using System.Linq;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class TemporalFindingDeduplicatorContinuityTests
{
    private static readonly EvidenceVector Evidence = new(YoloConf: 0.8);

    public TemporalFindingDeduplicatorContinuityTests()
    {
        VsaResolverTestCatalog.ConfigureDefault();
    }

    [Fact]
    public void Update_VertauschteReihenfolge_BehaeltGetrennteQuantifizierungUndSchwere()
    {
        var deduplicator = CreateDeduplicator();
        var left = Finding(0.05, 20, 1);
        var right = Finding(0.70, 80, 4);

        Assert.Empty(deduplicator.Update(new[] { left, right }, 5.0, Evidence));
        Assert.Empty(deduplicator.Update(new[] { right, left }, 5.2, Evidence));

        var detections = deduplicator.Flush();
        Assert.Equal(2, detections.Count);
        AssertFinding(detections.Single(d => d.ExtentPercent == 20), 20, 1, 2);
        AssertFinding(detections.Single(d => d.ExtentPercent == 80), 80, 4, 2);
    }

    [Fact]
    public void Update_TrefferVerschwindetKurzUndKehrtZurueck_BehaeltBeideIdentitaeten()
    {
        var deduplicator = CreateDeduplicator();
        var left = Finding(0.05, 20, 1);
        var right = Finding(0.70, 80, 4);

        deduplicator.Update(new[] { left, right }, 5.0, Evidence);
        Assert.Empty(deduplicator.Update(new[] { right }, 5.2, Evidence));
        Assert.Empty(deduplicator.Update(new[] { right, left }, 5.4, Evidence));

        var detections = deduplicator.Flush();
        Assert.Equal(2, detections.Count);
        AssertFinding(detections.Single(d => d.ExtentPercent == 20), 20, 1, 2);
        AssertFinding(detections.Single(d => d.ExtentPercent == 80), 80, 4, 3);
    }

    [Fact]
    public void Update_GleicherCodeUndUhrlageAberNeueBildposition_StartetGetrenntenBefund()
    {
        var deduplicator = CreateDeduplicator();

        deduplicator.Update(new[] { Finding(0.05, 20, 1) }, 5.0, Evidence);
        Assert.Empty(deduplicator.Update(new[] { Finding(0.70, 80, 4) }, 5.2, Evidence));

        var detections = deduplicator.Flush();
        Assert.Equal(2, detections.Count);
        AssertFinding(detections.Single(d => d.ExtentPercent == 20), 20, 1, 1);
        AssertFinding(detections.Single(d => d.ExtentPercent == 80), 80, 4, 1);
    }

    [Fact]
    public void Update_EinzelnerLeichtBewegterTreffer_FuehrtBeobachtungenZusammen()
    {
        var deduplicator = CreateDeduplicator();

        deduplicator.Update(new[] { Finding(0.05, 20, 1) }, 5.0, Evidence);
        Assert.Empty(deduplicator.Update(new[] { Finding(0.08, 30, 2) }, 5.2, Evidence));
        Assert.Empty(deduplicator.Update(new[] { Finding(0.11, 25, 1) }, 5.4, Evidence));

        AssertFinding(Assert.Single(deduplicator.Flush()), 30, 2, 3);
    }

    [Fact]
    public void Update_EinTrefferBleibtVerschwunden_SchliesstNurDiesenNachZeitfensterAb()
    {
        var deduplicator = CreateDeduplicator();
        var left = Finding(0.05, 20, 1);
        var right = Finding(0.70, 80, 4);

        deduplicator.Update(new[] { left, right }, 5.0, Evidence);
        Assert.Empty(deduplicator.Update(new[] { right }, 5.2, Evidence));
        Assert.Empty(deduplicator.Update(new[] { right }, 5.4, Evidence));
        var completed = deduplicator.Update(new[] { right }, 5.6, Evidence);

        AssertFinding(Assert.Single(completed), 20, 1, 1);
        AssertFinding(Assert.Single(deduplicator.Flush()), 80, 4, 4);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Update_KurzFehlendeOderUngueltigeBox_BewahrtLetzteBrauchbarePosition(double? invalidX1)
    {
        var deduplicator = CreateDeduplicator();

        deduplicator.Update(new[] { Finding(0.05, 20, 1) }, 5.0, Evidence);
        deduplicator.Update(new[] { Finding(0.05, 25, 2) with { BboxX1 = invalidX1 } }, 5.2, Evidence);
        deduplicator.Update(new[] { Finding(0.70, 80, 4) }, 5.4, Evidence);

        var detections = deduplicator.Flush();
        Assert.Equal(2, detections.Count);
        AssertFinding(detections.Single(d => d.ExtentPercent == 25), 25, 2, 2);
        AssertFinding(detections.Single(d => d.ExtentPercent == 80), 80, 4, 1);
    }

    [Fact]
    public void Update_MehrereAktiveTrefferUndNeueFehlendeBox_ErfindetKeineRaumzuordnung()
    {
        var deduplicator = CreateDeduplicator();
        var unknown = Finding(0.05, 50, 3) with
        {
            BboxX1 = null, BboxY1 = null, BboxX2 = null, BboxY2 = null
        };

        deduplicator.Update(new[] { Finding(0.05, 20, 1), Finding(0.70, 80, 4) }, 5.0, Evidence);
        deduplicator.Update(new[] { unknown }, 5.2, Evidence);

        var detections = deduplicator.Flush();
        Assert.Equal(3, detections.Count);
        AssertFinding(detections.Single(d => d.ExtentPercent == 20), 20, 1, 1);
        AssertFinding(detections.Single(d => d.ExtentPercent == 80), 80, 4, 1);
        AssertFinding(detections.Single(d => d.ExtentPercent == 50), 50, 3, 1);
    }

    private static TemporalFindingDeduplicator CreateDeduplicator() =>
        new(new TemporalDedupOptions { DedupWindowFrames = 3 });

    private static void AssertFinding(RawVideoDetection detection, int extent, int severity, int frameCount)
    {
        Assert.Equal("BCC", detection.VsaCodeHint);
        Assert.Equal("3:00", detection.PositionClock);
        Assert.Equal(extent, detection.ExtentPercent);
        Assert.Equal(severity, detection.SeverityLevel);
        Assert.Equal(extent / 2, detection.WidthMm);
        Assert.NotNull(detection.Evidence);
        Assert.Equal(frameCount, detection.Evidence.FrameCount);
    }

    private static EnhancedFinding Finding(double x1, int extent, int severity) =>
        new(
            Label: "Oberflaechenschaden",
            VsaCodeHint: "BCC",
            Severity: severity,
            PositionClock: "3:00",
            ExtentPercent: extent,
            HeightMm: null,
            WidthMm: extent / 2,
            IntrusionPercent: null,
            CrossSectionReductionPercent: null,
            DiameterReductionMm: null,
            BboxX1: x1,
            BboxY1: 0.10,
            BboxX2: x1 + 0.20,
            BboxY2: 0.30,
            Notes: null);
}
