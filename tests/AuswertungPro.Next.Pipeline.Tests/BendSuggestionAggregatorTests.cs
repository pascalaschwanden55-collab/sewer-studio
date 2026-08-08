using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Fasst Einzelbild-Treffer zu Vorschlaegen zusammen. Die Regeln stammen aus der
/// Videomessung vom 2026-08-07 und der menschlichen Blindpruefung: Arbeitspunkt
/// 0,50, Zusammenfassung ueber den Meterstand statt ueber die Zeit, Schacht-
/// einfahrt auslassen.
/// </summary>
public sealed class BendSuggestionAggregatorTests
{
    [Fact]
    public void Treffer_unter_dem_Arbeitspunkt_werden_verworfen()
    {
        // conf 0,50 ist gemessen: halbe Fehlalarmlast bei gleichem Recall wie 0,25.
        var vorschlaege = Aggregiere(
            new BendFrameDetection(20, 5.0, 0.49),
            new BendFrameDetection(21, 5.1, 0.51));

        var einziger = Assert.Single(vorschlaege);
        Assert.Equal(1, einziger.FrameCount);
        Assert.Equal(0.51, einziger.MaxConfidence, 3);
    }

    [Fact]
    public void Die_Schachteinfahrt_wird_ausgelassen()
    {
        // Der Blick vom Schacht ins Rohr sieht aus wie ein Bogen und ist keiner.
        var vorschlaege = Aggregiere(
            new BendFrameDetection(1, 0.05, 0.90),
            new BendFrameDetection(30, 12.0, 0.90));

        var einziger = Assert.Single(vorschlaege);
        Assert.Equal(12.0, einziger.MeterStart!.Value, 3);
    }

    [Fact]
    public void Ohne_Meterstand_entscheidet_die_Anfangszeit_ueber_die_Schachteinfahrt()
    {
        var vorschlaege = Aggregiere(
            new BendFrameDetection(1, null, 0.90),
            new BendFrameDetection(40, null, 0.90));

        var einziger = Assert.Single(vorschlaege);
        Assert.Equal(40, einziger.PeakTimeSeconds, 3);
    }

    [Fact]
    public void Aufeinanderfolgende_Meter_werden_zu_einem_Vorschlag()
    {
        var vorschlaege = Aggregiere(
            new BendFrameDetection(20, 7.0, 0.55),
            new BendFrameDetection(21, 7.4, 0.81),
            new BendFrameDetection(22, 7.8, 0.60));

        var einziger = Assert.Single(vorschlaege);
        Assert.Equal(7.0, einziger.MeterStart!.Value, 3);
        Assert.Equal(7.8, einziger.MeterEnd!.Value, 3);
        Assert.Equal(0.81, einziger.MaxConfidence, 3);
        Assert.Equal(21, einziger.PeakTimeSeconds, 3);
        Assert.Equal(3, einziger.FrameCount);
    }

    [Fact]
    public void Ein_Sprung_ueber_einen_Meter_beginnt_einen_neuen_Vorschlag()
    {
        var vorschlaege = Aggregiere(
            new BendFrameDetection(20, 7.0, 0.60),
            new BendFrameDetection(21, 8.5, 0.60));

        Assert.Equal(2, vorschlaege.Count);
    }

    [Fact]
    public void Dieselbe_Stelle_bei_erneuter_Kamerafahrt_bleibt_ein_Vorschlag()
    {
        // Der wichtigste Fall: Die Kamera faehrt zurueck und noch einmal an. Ueber
        // die Zeit gerechnet waeren das drei Meldungen — genau dieses Artefakt hat
        // die Fehlalarmlast von 1,0 auf 2,8 je Haltung aufgeblaeht.
        var vorschlaege = Aggregiere(
            new BendFrameDetection(120, 6.9, 0.55),
            new BendFrameDetection(180, 7.1, 0.72),
            new BendFrameDetection(240, 7.0, 0.58));

        var einziger = Assert.Single(vorschlaege);
        Assert.Equal(3, einziger.FrameCount);
        Assert.Equal(0.72, einziger.MaxConfidence, 3);
    }

    [Fact]
    public void Ein_geschaetzter_Meterstand_darf_nicht_wie_ein_gelesener_zaehlen()
    {
        // VideoFullAnalysisService schaetzt den Meter linear aus der Zeit, wenn das
        // OSD nicht lesbar ist. Ein geschaetzter Meter waechst immer monoton — eine
        // zurueckgesetzte Kamera bekaeme dieselbe Stelle mit steigendem Meter und
        // wuerde faelschlich als neue Stelle gelten. Deshalb gilt hier die Zeitregel.
        var vorschlaege = Aggregiere(
            new BendFrameDetection(120, 6.9, 0.55, MeterIsEstimated: true),
            new BendFrameDetection(180, 7.1, 0.72, MeterIsEstimated: true));

        Assert.Equal(2, vorschlaege.Count);
        Assert.All(vorschlaege, vorschlag => Assert.True(vorschlag.MeterIsEstimated));
    }

    [Fact]
    public void Ein_geschaetzter_Meterstand_bleibt_als_grobe_Lage_erhalten()
    {
        // Der Wert ist unsicher, aber nicht wertlos — er sagt dem Menschen, wo
        // ungefaehr zu schauen ist. Er muss nur als geschaetzt gekennzeichnet sein.
        var vorschlaege = Aggregiere(
            new BendFrameDetection(120, 6.9, 0.55, MeterIsEstimated: true),
            new BendFrameDetection(121, 7.0, 0.60, MeterIsEstimated: true));

        var einziger = Assert.Single(vorschlaege);
        Assert.Equal(6.9, einziger.MeterStart!.Value, 3);
        Assert.Equal(7.0, einziger.MeterEnd!.Value, 3);
        Assert.True(einziger.MeterIsEstimated);
    }

    [Fact]
    public void Ein_gelesener_Meterstand_wird_nicht_als_geschaetzt_gemeldet()
    {
        var einziger = Assert.Single(Aggregiere(new BendFrameDetection(20, 5.0, 0.60)));

        Assert.False(einziger.MeterIsEstimated);
    }

    [Fact]
    public void Ohne_Meterstand_faellt_die_Zusammenfassung_auf_die_Zeit_zurueck()
    {
        var vorschlaege = Aggregiere(
            new BendFrameDetection(100, null, 0.60),
            new BendFrameDetection(102, null, 0.65),
            new BendFrameDetection(140, null, 0.60));

        Assert.Equal(2, vorschlaege.Count);
        Assert.Equal(2, vorschlaege[0].FrameCount);
    }

    [Fact]
    public void Ab_0_70_gilt_ein_Vorschlag_als_stark()
    {
        // In der Messung war oberhalb 0,70 kein einziger Fehlalarm dabei.
        var stark = Aggregiere(new BendFrameDetection(20, 5.0, 0.70));
        var schwach = Aggregiere(new BendFrameDetection(20, 5.0, 0.69));

        Assert.Equal(BendSuggestionStrength.Strong, stark[0].Strength);
        Assert.Equal(BendSuggestionStrength.Weak, schwach[0].Strength);
    }

    [Fact]
    public void Unsortierte_Eingaben_liefern_dasselbe_Ergebnis()
    {
        var sortiert = Aggregiere(
            new BendFrameDetection(20, 7.0, 0.55),
            new BendFrameDetection(21, 7.4, 0.81));
        var unsortiert = Aggregiere(
            new BendFrameDetection(21, 7.4, 0.81),
            new BendFrameDetection(20, 7.0, 0.55));

        Assert.Equal(sortiert.Count, unsortiert.Count);
        Assert.Equal(sortiert[0].MeterStart!.Value, unsortiert[0].MeterStart!.Value, 3);
        Assert.Equal(sortiert[0].MeterEnd!.Value, unsortiert[0].MeterEnd!.Value, 3);
    }

    [Fact]
    public void Ohne_Treffer_entsteht_kein_Vorschlag()
    {
        Assert.Empty(BendSuggestionAggregator.Aggregate(null, new BendSuggestionOptions()));
        Assert.Empty(Aggregiere());
    }

    [Fact]
    public void Vorschlaege_sind_nach_Meter_geordnet()
    {
        var vorschlaege = Aggregiere(
            new BendFrameDetection(200, 12.0, 0.60),
            new BendFrameDetection(20, 3.0, 0.60));

        Assert.Equal(3.0, vorschlaege[0].MeterStart!.Value, 3);
        Assert.Equal(12.0, vorschlaege[1].MeterStart!.Value, 3);
    }

    private static IReadOnlyList<BendSuggestion> Aggregiere(params BendFrameDetection[] treffer)
        => BendSuggestionAggregator.Aggregate(treffer, new BendSuggestionOptions());
}
