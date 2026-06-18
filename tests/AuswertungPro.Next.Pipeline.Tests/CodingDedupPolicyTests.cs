using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingDedupPolicyTests
{
    [Theory]
    [InlineData("BCD")]
    [InlineData("BCDA")]
    [InlineData("BCE")]
    [InlineData("BDC")]
    [InlineData("bdcxx")]
    public void IsOneTimeCode_RecognizesStartEndAndAbortCodes(string code)
    {
        Assert.True(CodingDedupPolicy.IsOneTimeCode(code));
    }

    [Theory]
    [InlineData("BCAEA", "BCA")]
    [InlineData("bcaea", "BCAAA")]
    [InlineData("BCD", "BCDA")]
    public void CodesMatch_UsesExactOrMainCode(string existingCode, string newCode)
    {
        Assert.True(CodingDedupPolicy.CodesMatch(existingCode, newCode));
    }

    [Fact]
    public void CodesMatch_RejectsDifferentMainCodes()
    {
        Assert.False(CodingDedupPolicy.CodesMatch("BCA", "BCC"));
    }

    [Fact]
    public void ShouldStopAnalysisAfterTerminalCode_StopsAtRohrendeMeter()
    {
        Assert.False(CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            [("BCE", 12.5, null)],
            currentMeter: 12.39,
            currentVideoTime: null));

        Assert.True(CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            [("BCE", 12.5, null)],
            currentMeter: 12.5,
            currentVideoTime: null));

        Assert.True(CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            [("BCE", 12.5, null)],
            currentMeter: 12.8,
            currentVideoTime: null));
    }

    [Fact]
    public void ShouldStopAnalysisAfterTerminalCode_StopsAfterAbortOrTimeWhenMeterMissing()
    {
        Assert.True(CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            [("BDC", null, TimeSpan.FromSeconds(80))],
            currentMeter: null,
            currentVideoTime: TimeSpan.FromSeconds(81)));

        Assert.False(CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
            [("BCE", null, TimeSpan.FromSeconds(80))],
            currentMeter: null,
            currentVideoTime: TimeSpan.FromSeconds(79)));
    }

    [Fact]
    public void ShouldDeferSpatialCodeUntilCloser_DeferiertNurVorausBogen()
    {
        var previewBend = MetrierungProximityEvaluator.Evaluate(
            new MetrierungProximityInput(0.46, 0.46, 0.54, 0.54, 0.5, 0.5, 1.0, 0.5),
            MetrierungProximityThresholds.Default);

        Assert.False(previewBend.IsCodierbar);
        Assert.True(CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser("BCC", previewBend));
    }

    [Fact]
    public void ShouldDeferSpatialCodeUntilCloser_CodiertQuerschnittsfuellendenBogen()
    {
        var realBend = MetrierungProximityEvaluator.Evaluate(
            new MetrierungProximityInput(0.03, 0.03, 0.97, 0.97, 0.5, 0.5, 1.0, 0.5),
            MetrierungProximityThresholds.Default);

        Assert.True(realBend.IsCodierbar);
        Assert.False(CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser("BCC", realBend));
    }

    [Fact]
    public void ShouldDeferSpatialCodeUntilCloser_CodiertBogenWennNichtZentralVoraus()
    {
        var nearWallBend = MetrierungProximityEvaluator.Evaluate(
            new MetrierungProximityInput(0.46, 0.02, 0.54, 0.12, 0.5, 0.5, 1.0, 0.5),
            MetrierungProximityThresholds.Default);

        Assert.True(nearWallBend.IsCodierbar);
        Assert.False(CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser("BCC", nearWallBend));
        Assert.False(CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser("BAB", nearWallBend));
    }

    [Fact]
    public void IsBoundaryEndCodePlausible_BCE_weit_vor_dem_Ende_ist_unplausibel()
    {
        // Haltung 45m, Kamera bei 28.68m -> Rohrende noch weit weg -> verwerfen.
        Assert.False(CodingDedupPolicy.IsBoundaryEndCodePlausible("BCE", currentMeter: 28.68, endMeter: 45.0));
    }

    [Fact]
    public void IsBoundaryEndCodePlausible_BCE_nahe_am_Ende_ist_plausibel()
    {
        // Kamera bei 44m bei 45m-Haltung -> nah genug (ueber 90%) -> akzeptieren.
        Assert.True(CodingDedupPolicy.IsBoundaryEndCodePlausible("BCE", currentMeter: 44.0, endMeter: 45.0));
    }

    [Fact]
    public void IsBoundaryEndCodePlausible_BCE_lange_Haltung_90Prozent_Schwelle()
    {
        // Lange Haltung 100m: ab 90% (90m) plausibel; knapp darunter (88m) noch nicht.
        Assert.True(CodingDedupPolicy.IsBoundaryEndCodePlausible("BCE", currentMeter: 92.0, endMeter: 100.0));
        Assert.False(CodingDedupPolicy.IsBoundaryEndCodePlausible("BCE", currentMeter: 88.0, endMeter: 100.0));
    }

    [Fact]
    public void IsBoundaryEndCodePlausible_BCE_kurze_Haltung_letzte_20cm()
    {
        // Sehr kurze Haltung 1.5m: 90% (1.35m) waere fast die ganze Strecke; die absolute
        // 20-cm-Regel (ab 1.30m) ist hier die massgebliche, frueher erreichte Schwelle.
        Assert.True(CodingDedupPolicy.IsBoundaryEndCodePlausible("BCE", currentMeter: 1.32, endMeter: 1.5));
        Assert.False(CodingDedupPolicy.IsBoundaryEndCodePlausible("BCE", currentMeter: 1.20, endMeter: 1.5));
    }

    [Fact]
    public void IsBoundaryEndCodePlausible_BCE_ohne_bekanntes_Ende_ist_plausibel()
    {
        // EndMeter unbekannt/0 -> konservativ akzeptieren (sonst entstuende evtl. gar kein Rohrende).
        Assert.True(CodingDedupPolicy.IsBoundaryEndCodePlausible("BCE", currentMeter: 12.0, endMeter: 0));
        Assert.True(CodingDedupPolicy.IsBoundaryEndCodePlausible("BCE", currentMeter: 12.0, endMeter: null));
    }

    [Fact]
    public void IsBoundaryEndCodePlausible_BCE_ohne_bekannte_Position_ist_plausibel()
    {
        Assert.True(CodingDedupPolicy.IsBoundaryEndCodePlausible("BCE", currentMeter: null, endMeter: 45.0));
    }

    [Fact]
    public void IsBoundaryEndCodePlausible_BCE_weit_UEBER_dem_Ende_ist_unplausibel()
    {
        // Realfall: BCE-Meter 114.13 bei 15.82m-Haltung (kaputter OSD-Wert) -> verwerfen.
        Assert.False(CodingDedupPolicy.IsBoundaryEndCodePlausible("BCE", currentMeter: 114.13, endMeter: 15.82));
    }

    [Fact]
    public void IsBoundaryEndCodePlausible_BCE_knapp_ueber_Ende_innerhalb_Toleranz_ist_plausibel()
    {
        // 15.82 + 0.5 = 16.32 <= Ende+1.0 -> noch plausibel (Messunschaerfe am Ende).
        Assert.True(CodingDedupPolicy.IsBoundaryEndCodePlausible("BCE", currentMeter: 16.32, endMeter: 15.82));
    }

    [Fact]
    public void ResolvePlausibleEndMeter_korrigiert_kaputten_OSD_auf_Import()
    {
        // OSD 114.13 unplausibel -> Import-BCE 15.82 gewinnt.
        Assert.Equal(15.82, CodingDedupPolicy.ResolvePlausibleEndMeter(114.13, importEndMeter: 15.82, vmEndMeter: 15.82));
    }

    [Fact]
    public void ResolvePlausibleEndMeter_behaelt_plausiblen_OSD()
    {
        // OSD 15.70 plausibel nah am Ende -> beibehalten.
        Assert.Equal(15.70, CodingDedupPolicy.ResolvePlausibleEndMeter(15.70, importEndMeter: 15.82, vmEndMeter: 15.82));
    }

    [Fact]
    public void ResolvePlausibleEndMeter_ohne_verlaessliches_Ende_nimmt_OSD()
    {
        Assert.Equal(42.0, CodingDedupPolicy.ResolvePlausibleEndMeter(42.0, importEndMeter: null, vmEndMeter: 0));
    }

    [Fact]
    public void ResolvePlausibleEndMeter_faellt_ohne_OSD_auf_verlaessliches_Ende()
    {
        Assert.Equal(15.82, CodingDedupPolicy.ResolvePlausibleEndMeter(null, importEndMeter: 15.82, vmEndMeter: 20.0));
    }

    [Theory]
    [InlineData("BCD")]
    [InlineData("BAB")]
    [InlineData("BCC")]
    public void IsBoundaryEndCodePlausible_NurBCE_wird_geprueft(string code)
    {
        // Andere Codes (auch Rohranfang BCD) sind hier immer plausibel.
        Assert.True(CodingDedupPolicy.IsBoundaryEndCodePlausible(code, currentMeter: 5.0, endMeter: 45.0));
    }
}
