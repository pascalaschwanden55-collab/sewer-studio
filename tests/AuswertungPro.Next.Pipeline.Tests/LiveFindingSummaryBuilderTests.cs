using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer <see cref="LiveFindingSummaryBuilder"/>.
/// </summary>
public sealed class LiveFindingSummaryBuilderTests
{
    // ── BuildFrameInfo ───────────────────────────────────────────────────────

    [Fact]
    public void BuildFrameInfo_MitMeter_EnthalteMeterText()
    {
        var result = LiveFindingSummaryBuilder.BuildFrameInfo(42, 200, "12.5 m");
        Assert.Equal("Frame 42/200  |  Meter 12.5 m", result);
    }

    [Fact]
    public void BuildFrameInfo_OhneMeter_ZeigtDash()
    {
        var result = LiveFindingSummaryBuilder.BuildFrameInfo(0, 0, null);
        Assert.Equal("Frame 0/0  |  Meter —", result);
    }

    [Fact]
    public void BuildFrameInfo_LeererMeter_ZeigtDash()
    {
        var result = LiveFindingSummaryBuilder.BuildFrameInfo(5, 10, "   ");
        Assert.Equal("Frame 5/10  |  Meter —", result);
    }

    [Fact]
    public void BuildFrameInfo_NegativTotal_KlemmtAufNull()
    {
        // totalFrames < 0 sollte auf 0 geclamppt werden
        var result = LiveFindingSummaryBuilder.BuildFrameInfo(0, -5, "1.0 m");
        Assert.Equal("Frame 0/0  |  Meter 1.0 m", result);
    }

    // ── BuildQuantSummary ────────────────────────────────────────────────────

    [Fact]
    public void BuildQuantSummary_LeereListe_GibtPlatzhalterText()
    {
        var result = LiveFindingSummaryBuilder.BuildQuantSummary(new List<LiveFrameFinding>());
        Assert.Equal("Quantifizierung: keine Punkte erkannt", result);
    }

    [Fact]
    public void BuildQuantSummary_EinBefundMitUhrlage_EnthaltePfx()
    {
        var findings = new List<LiveFrameFinding>
        {
            new("Riss", 3, "6", 25)
        };
        var result = LiveFindingSummaryBuilder.BuildQuantSummary(findings);
        Assert.StartsWith("Q: ", result);
        Assert.Contains("6", result);
        Assert.Contains("25%", result);
    }

    [Fact]
    public void BuildQuantSummary_OhneQuantDaten_ZeigtNa()
    {
        var findings = new List<LiveFrameFinding>
        {
            new("Test", 1, null, null)
        };
        var result = LiveFindingSummaryBuilder.BuildQuantSummary(findings);
        Assert.Contains("n/a", result);
        Assert.Contains("?", result); // Uhrlage unbekannt
    }

    [Fact]
    public void BuildQuantSummary_MaxVierBefunde_AusgabeMaxVier()
    {
        // 5 Befunde -> nur 4 werden ausgegeben
        var findings = new List<LiveFrameFinding>
        {
            new("A", 1, "1", 10),
            new("B", 2, "2", 20),
            new("C", 3, "3", 30),
            new("D", 4, "4", 40),
            new("E", 5, "5", 50)   // dieser soll nicht in der Ausgabe erscheinen
        };
        var result = LiveFindingSummaryBuilder.BuildQuantSummary(findings);
        // 4 Trennzeichen " | " fuer 4 Elemente -> 3 Vorkommen
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(result, @"\s\|\s").Count);
    }

    [Fact]
    public void BuildQuantSummary_MitAllQuant_EnthaltAlleFelder()
    {
        var findings = new List<LiveFrameFinding>
        {
            new("Riss", 3, "3", 15,
                HeightMm: 12, WidthMm: 8, IntrusionPercent: 5,
                CrossSectionReductionPercent: 20, DiameterReductionMm: 3)
        };
        var result = LiveFindingSummaryBuilder.BuildQuantSummary(findings);
        Assert.Contains("H:12mm", result);
        Assert.Contains("B:8mm", result);
        Assert.Contains("Einr:5%", result);
        Assert.Contains("QV:20%", result);
        Assert.Contains("DV:3mm", result);
    }

    // ── BuildFindingLabel ────────────────────────────────────────────────────

    [Fact]
    public void BuildFindingLabel_MitCode_EnthaltCodeUndLabel()
    {
        var finding = new LiveFrameFinding("Korrosion", 3, "6", 30, VsaCodeHint: "BAF");
        var result = LiveFindingSummaryBuilder.BuildFindingLabel(finding);
        Assert.StartsWith("6 / 30%", result);
        Assert.Contains("BAF Korrosion", result);
    }

    [Fact]
    public void BuildFindingLabel_OhneCode_NurLabel()
    {
        var finding = new LiveFrameFinding("Riss", 2, "3", null);
        var result = LiveFindingSummaryBuilder.BuildFindingLabel(finding);
        Assert.Contains("Riss", result);
        Assert.Contains("n/a", result); // kein Extent
    }

    [Fact]
    public void BuildFindingLabel_LangesLabel_WirdAbgeschnitten()
    {
        var finding = new LiveFrameFinding("Ein sehr langer Befundtext der ueberschritten wird", 1, "12", null);
        var result = LiveFindingSummaryBuilder.BuildFindingLabel(finding);
        Assert.Contains("...", result);
    }

    [Fact]
    public void BuildFindingLabel_OhneUhrlage_ZeigtFragezeichen()
    {
        var finding = new LiveFrameFinding("Test", 1, null, null);
        var result = LiveFindingSummaryBuilder.BuildFindingLabel(finding);
        Assert.StartsWith("?", result);
    }

    [Fact]
    public void BuildFindingLabel_MitEinragung_EnthaltEinr()
    {
        var finding = new LiveFrameFinding("Einragung", 3, "9", 10, IntrusionPercent: 15);
        var result = LiveFindingSummaryBuilder.BuildFindingLabel(finding);
        Assert.Contains("Einr:15%", result);
    }
}
