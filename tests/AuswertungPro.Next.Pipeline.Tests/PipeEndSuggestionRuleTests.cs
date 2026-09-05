using System;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Die Regel der Freigabe vom 2026-08-12 fuer Rohranfang und Rohrende:
/// "Die staerkste gruppierte Meldung des Modells im GANZEN Video. Kein
/// Zeitfenster." Ein Video hat genau einen Rohranfang und genau ein Rohrende,
/// deshalb gibt es je Klasse hoechstens EINEN Vorschlag.
///
/// Die Gruppierung ist dieselbe wie im Abnahmeskript
/// (training/scripts/lernstufe_videolauf.py, zusammenfassen): gesammelt wird ab
/// dem Boden 0,10, eine Luecke ueber 3 s trennt zwei Stellen, gezaehlt wird die
/// fertige Stelle erst ab der Schwelle 0,50. Weicht die Regel hier ab, gelten
/// die gemessenen 85/98 % (Anfang) und 89/88 % (Ende) nicht mehr.
/// </summary>
public sealed class PipeEndSuggestionRuleTests
{
    [Fact]
    public void Ohne_Bild_ueber_der_Schwelle_gibt_es_keinen_Vorschlag()
    {
        var ergebnis = PipeEndSuggestionRule.Strongest(
            Bilder((10, 0.30), (11, 0.45), (12, 0.20)),
            PipeEndKind.Rohranfang,
            new PipeEndRuleOptions());

        Assert.Null(ergebnis);
    }

    [Fact]
    public void Bilder_innerhalb_von_drei_Sekunden_bilden_eine_Stelle()
    {
        var ergebnis = PipeEndSuggestionRule.Strongest(
            Bilder((10, 0.60), (11, 0.90), (12, 0.70)),
            PipeEndKind.Rohranfang,
            new PipeEndRuleOptions());

        Assert.NotNull(ergebnis);
        Assert.Equal(PipeEndKind.Rohranfang, ergebnis.Kind);
        Assert.Equal(10.0, ergebnis.TimeStartSeconds);
        Assert.Equal(12.0, ergebnis.TimeEndSeconds);
        Assert.Equal(11.0, ergebnis.PeakTimeSeconds);
        Assert.Equal(0.90, ergebnis.MaxConfidence, 3);
        Assert.Equal(3, ergebnis.FrameCount);
    }

    [Fact]
    public void Die_staerkste_Stelle_gewinnt_nicht_die_erste()
    {
        // Genau der Fehler des alten Zeitfensters beim Rohrende: Die richtige
        // Meldung lag mit ~1,00 spaeter im Video, die erste war schwaecher.
        var ergebnis = PipeEndSuggestionRule.Strongest(
            Bilder((10, 0.60), (11, 0.55), (40, 0.80), (41, 0.95), (42, 0.70)),
            PipeEndKind.Rohrende,
            new PipeEndRuleOptions());

        Assert.NotNull(ergebnis);
        Assert.Equal(41.0, ergebnis.PeakTimeSeconds);
        Assert.Equal(40.0, ergebnis.TimeStartSeconds);
        Assert.Equal(42.0, ergebnis.TimeEndSeconds);
        Assert.Equal(0.95, ergebnis.MaxConfidence, 3);
    }

    [Fact]
    public void Bei_gleicher_Konfidenz_gewinnt_die_fruehere_Stelle()
    {
        var ergebnis = PipeEndSuggestionRule.Strongest(
            Bilder((10, 0.90), (40, 0.90)),
            PipeEndKind.Rohranfang,
            new PipeEndRuleOptions());

        Assert.NotNull(ergebnis);
        Assert.Equal(10.0, ergebnis.PeakTimeSeconds);
    }

    [Fact]
    public void Eine_Luecke_ueber_drei_Sekunden_trennt_zwei_Stellen()
    {
        // 10 -> 14 sind vier Sekunden: zwei Stellen, die staerkere (fruehere bei
        // Gleichstand) gewinnt und zaehlt nur ihre eigenen Bilder.
        var ergebnis = PipeEndSuggestionRule.Strongest(
            Bilder((10, 0.90), (14, 0.90)),
            PipeEndKind.Rohranfang,
            new PipeEndRuleOptions());

        Assert.NotNull(ergebnis);
        Assert.Equal(10.0, ergebnis.PeakTimeSeconds);
        Assert.Equal(10.0, ergebnis.TimeEndSeconds);
        Assert.Equal(1, ergebnis.FrameCount);
    }

    [Fact]
    public void Ein_Einbruch_ueber_dem_Boden_zerlegt_die_Stelle_nicht()
    {
        // Gesammelt wird ab dem Boden 0,10: Ein Einbruch auf 0,20 zwischen zwei
        // starken Bildern bleibt Teil derselben Stelle (Bogenmessung 2026-08-08).
        var ergebnis = PipeEndSuggestionRule.Strongest(
            Bilder((10, 0.90), (11, 0.20), (12, 0.85)),
            PipeEndKind.Rohranfang,
            new PipeEndRuleOptions());

        Assert.NotNull(ergebnis);
        Assert.Equal(3, ergebnis.FrameCount);
        Assert.Equal(12.0, ergebnis.TimeEndSeconds);
    }

    [Fact]
    public void Ein_Bild_unter_dem_Boden_verbindet_keine_Stellen()
    {
        // 0,05 liegt unter dem Boden und wird gar nicht gesammelt; 10 -> 14 bleibt
        // damit eine Luecke von vier Sekunden.
        var ergebnis = PipeEndSuggestionRule.Strongest(
            Bilder((10, 0.90), (12, 0.05), (14, 0.60)),
            PipeEndKind.Rohranfang,
            new PipeEndRuleOptions());

        Assert.NotNull(ergebnis);
        Assert.Equal(10.0, ergebnis.PeakTimeSeconds);
        Assert.Equal(1, ergebnis.FrameCount);
    }

    [Fact]
    public void Eine_Stelle_sammelt_ab_dem_Boden_und_zaehlt_erst_ab_der_Schwelle()
    {
        var ergebnis = PipeEndSuggestionRule.Strongest(
            Bilder((10, 0.15), (11, 0.55), (12, 0.20)),
            PipeEndKind.Rohranfang,
            new PipeEndRuleOptions());

        Assert.NotNull(ergebnis);
        Assert.Equal(10.0, ergebnis.TimeStartSeconds);
        Assert.Equal(12.0, ergebnis.TimeEndSeconds);
        Assert.Equal(11.0, ergebnis.PeakTimeSeconds);
        Assert.Equal(3, ergebnis.FrameCount);
    }

    [Fact]
    public void Beim_Rohrende_werden_die_ersten_drei_Sekunden_ausgeblendet()
    {
        // Der Schacht am Videoanfang sieht wie ein Rohrende aus (Abnahme: ab_sekunde 3).
        var ergebnis = PipeEndSuggestionRule.Strongest(
            Bilder((1, 0.99), (2, 0.99), (50, 0.80)),
            PipeEndKind.Rohrende,
            PipeEndRuleOptions.ForKind(PipeEndKind.Rohrende));

        Assert.NotNull(ergebnis);
        Assert.Equal(50.0, ergebnis.PeakTimeSeconds);
    }

    [Fact]
    public void Der_Rohranfang_darf_auf_der_ersten_Sekunde_liegen()
    {
        // Fuer die Klasse, die GENAU am Anfang sitzt, wuerde das Ausblenden den
        // einzigen echten Treffer wegwerfen (Abnahme: ab_sekunde 0).
        var ergebnis = PipeEndSuggestionRule.Strongest(
            Bilder((0, 0.99), (1, 0.98)),
            PipeEndKind.Rohranfang,
            PipeEndRuleOptions.ForKind(PipeEndKind.Rohranfang));

        Assert.NotNull(ergebnis);
        Assert.Equal(0.0, ergebnis.PeakTimeSeconds);
        Assert.Equal(2, ergebnis.FrameCount);
    }

    [Fact]
    public void Unsortierte_Bilder_werden_nach_Zeit_gruppiert()
    {
        var ergebnis = PipeEndSuggestionRule.Strongest(
            Bilder((12, 0.70), (10, 0.60), (11, 0.90)),
            PipeEndKind.Rohranfang,
            new PipeEndRuleOptions());

        Assert.NotNull(ergebnis);
        Assert.Equal(10.0, ergebnis.TimeStartSeconds);
        Assert.Equal(12.0, ergebnis.TimeEndSeconds);
        Assert.Equal(3, ergebnis.FrameCount);
    }

    [Fact]
    public void Die_Vorgaben_je_Klasse_entsprechen_der_Abnahme()
    {
        var anfang = PipeEndRuleOptions.ForKind(PipeEndKind.Rohranfang);
        var ende = PipeEndRuleOptions.ForKind(PipeEndKind.Rohrende);

        Assert.Equal(0.0, anfang.SkipFirstSeconds);
        Assert.Equal(3.0, ende.SkipFirstSeconds);
        Assert.Equal(0.50, anfang.Threshold);
        Assert.Equal(0.10, anfang.FloorConfidence);
        Assert.Equal(3.0, anfang.TimeGapSeconds);
    }

    private static PipeEndFrameScore[] Bilder(params (double Zeit, double Wert)[] werte)
        => werte.Select(w => new PipeEndFrameScore(w.Zeit, w.Wert)).ToArray();
}
