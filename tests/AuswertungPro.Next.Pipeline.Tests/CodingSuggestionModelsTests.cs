using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Reine Regeln der Vorschlagsliste im Codiermodus: Pin, Zeilentext,
/// Meterspur-Nachschlag. Kein WPF, kein Sidecar.
/// </summary>
public sealed class CodingSuggestionModelsTests
{
    [Fact]
    public void Der_Bogen_Pin_ist_der_gemessene_Kandidat_des_Training_Studios()
    {
        Assert.Equal("bcc_nc15_seed46_20260808", CodingBendCandidatePin.Id);
        Assert.Equal(
            "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114",
            CodingBendCandidatePin.WeightSha256);
    }

    [Fact]
    public void Bogen_mit_gelesenem_Meter_zeigt_Meter_und_Staerke()
    {
        var zeile = CodingSuggestionText.Zeile(Bogen(meter: 9.42, geschaetzt: false, stark: true));
        Assert.Equal("Bogen · Meter 9,42 · stark", zeile);
    }

    [Fact]
    public void Bogen_mit_geschaetztem_Meter_sagt_ca()
    {
        var zeile = CodingSuggestionText.Zeile(Bogen(meter: 9.42, geschaetzt: true, stark: false));
        Assert.Equal("Bogen · Meter ca. 9,4 · schwach", zeile);
    }

    [Fact]
    public void Bogen_ohne_Meter_nennt_die_Sekunde_und_nie_null_Meter()
    {
        var zeile = CodingSuggestionText.Zeile(Bogen(meter: null, geschaetzt: false, stark: true) with { PeakTimeSeconds = 87.4 });
        Assert.Equal("Bogen · Sekunde 87 (Meterstand nicht lesbar) · stark", zeile);
        Assert.DoesNotContain("0,0", zeile);
    }

    [Fact]
    public void Rohranfang_und_Rohrende_nennen_Sekunde_und_Abnahmewert()
    {
        var anfang = new CodingSuggestion(CodingSuggestionKind.Rohranfang, 4.2, null, false, 0.97, true, 0.8545);
        var ende = new CodingSuggestion(CodingSuggestionKind.Rohrende, 143.0, 42.35, false, 0.91, true, 0.8889);

        Assert.Equal("Rohranfang · Sekunde 4 · Abnahme 85 %", CodingSuggestionText.Zeile(anfang));
        Assert.Equal("Rohrende · Sekunde 143 · Abnahme 89 %", CodingSuggestionText.Zeile(ende));
    }

    [Fact]
    public void Meter_Nachschlag_nimmt_den_naechsten_Punkt_innerhalb_der_Toleranz()
    {
        var spur = new List<MeterTrackPoint>
        {
            new(10.0, 5.00, false),
            new(11.0, 5.50, false),
            new(12.0, 6.00, true),
            new(20.0, 10.0, false)
        };

        var treffer = CodingSuggestionMeterLookup.Find(spur, 11.4);
        Assert.NotNull(treffer);
        Assert.Equal(11.0, treffer!.TimeSeconds);

        var geschaetzt = CodingSuggestionMeterLookup.Find(spur, 12.2);
        Assert.NotNull(geschaetzt);
        Assert.True(geschaetzt!.IsEstimated);

        Assert.Null(CodingSuggestionMeterLookup.Find(spur, 16.0));
        Assert.Null(CodingSuggestionMeterLookup.Find(Array.Empty<MeterTrackPoint>(), 11.0));
    }

    [Fact]
    public void Ein_leeres_Set_traegt_den_Grund_in_beiden_Teilen()
    {
        var leer = CodingSuggestionSet.Leer("ausgeschaltet");
        Assert.Empty(leer.Suggestions);
        Assert.Empty(leer.MeterTrack);
        Assert.Equal(CodingSuggestionPartStatus.NichtVerfuegbar, leer.BogenTeil.Status);
        Assert.Equal("ausgeschaltet", leer.AnfangEndeTeil.Grund);
    }

    private static CodingSuggestion Bogen(double? meter, bool geschaetzt, bool stark)
        => new(CodingSuggestionKind.Bogen, 30.0, meter, geschaetzt, stark ? 0.9 : 0.6, stark, 0.0);
}
