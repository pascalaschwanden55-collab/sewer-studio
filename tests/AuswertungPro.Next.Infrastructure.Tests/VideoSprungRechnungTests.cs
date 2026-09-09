using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Video;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Rueckweg der QGIS-Bruecke: Der Benutzer klickt in der Karte auf eine Stelle der
/// Haltung, und das Video soll dorthin springen. Aus dem Meterwert wird ueber
/// dieselben Stuetzstellen eine Videozeit — nur eben umgekehrt.
///
/// Wichtigster Schutz: Es wird nur in der Haltung gesprungen, die gerade laeuft.
/// Ein Klick auf eine Nachbarhaltung darf das offene Video nicht verstellen.
/// </summary>
public sealed class VideoSprungRechnungTests
{
    private static VideoMeterStuetzstelle Punkt(int sekunde, double meter)
        => new(TimeSpan.FromSeconds(sekunde), meter);

    private static VideoPositionEingang Eingang(
        string haltung = "80475-80462",
        double? laenge = 50.0,
        IReadOnlyList<VideoMeterStuetzstelle>? stellen = null,
        int dauerSekunden = 100)
        => new(
            haltung,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(dauerSekunden),
            Playing: true,
            laenge,
            stellen ?? Array.Empty<VideoMeterStuetzstelle>());

    private static VideoSprungAuftrag Auftrag(string haltung = "80475-80462", double meter = 25.0)
        => new(haltung, meter);

    [Fact]
    public void Ohne_laufendes_Video_wird_nicht_gesprungen()
    {
        var ergebnis = VideoSprungRechnung.Plane(null, Auftrag());

        Assert.False(ergebnis.Bereit);
        Assert.Equal(VideoSprungGrund.KeinVideo, ergebnis.Grund);
    }

    [Fact]
    public void Ohne_Auftrag_wird_nicht_gesprungen()
    {
        var ergebnis = VideoSprungRechnung.Plane(Eingang(), null);

        Assert.False(ergebnis.Bereit);
        Assert.Equal(VideoSprungGrund.KeinAuftrag, ergebnis.Grund);
    }

    [Fact]
    public void Eine_fremde_Haltung_verstellt_das_offene_Video_nicht()
    {
        var ergebnis = VideoSprungRechnung.Plane(
            Eingang(haltung: "80475-80462"),
            Auftrag(haltung: "80462-80455"));

        Assert.False(ergebnis.Bereit);
        Assert.Equal(VideoSprungGrund.FremdeHaltung, ergebnis.Grund);
    }

    [Fact]
    public void Die_Gegenrichtung_ist_eine_andere_Haltung()
    {
        // 80462-80475 ist die Gegenfahrt und hat eine eigene Aufnahme. Der Name darf
        // dafuer NIE normalisiert werden, sonst springt das falsche Video.
        var ergebnis = VideoSprungRechnung.Plane(
            Eingang(haltung: "80475-80462"),
            Auftrag(haltung: "80462-80475"));

        Assert.Equal(VideoSprungGrund.FremdeHaltung, ergebnis.Grund);
    }

    [Fact]
    public void Leerzeichen_und_Gross_Kleinschreibung_trennen_keine_Haltung()
    {
        var ergebnis = VideoSprungRechnung.Plane(
            Eingang(haltung: "80475-80462"),
            Auftrag(haltung: "  80475-80462 "));

        Assert.True(ergebnis.Bereit);
    }

    [Fact]
    public void Ohne_Stuetzstellen_rechnet_die_mittlere_Geschwindigkeit()
    {
        // 50 m in 100 s = 0,5 m/s. 25 m sind also Sekunde 50.
        var ergebnis = VideoSprungRechnung.Plane(Eingang(), Auftrag(meter: 25.0));

        Assert.True(ergebnis.Bereit);
        Assert.Equal(50.0, ergebnis.Zeit!.Value.TotalSeconds, 3);
        Assert.Equal(VideoMeterQuelle.MittlereGeschwindigkeit, ergebnis.Quelle);
    }

    [Fact]
    public void Zwischen_zwei_Stuetzstellen_wird_zurueck_interpoliert()
    {
        // Bei Sekunde 10 steht die Kamera bei 5 m, bei Sekunde 20 bei 15 m.
        // 10 m liegen genau in der Mitte, also Sekunde 15.
        var ergebnis = VideoSprungRechnung.Plane(
            Eingang(stellen: new[] { Punkt(10, 5.0), Punkt(20, 15.0) }),
            Auftrag(meter: 10.0));

        Assert.True(ergebnis.Bereit);
        Assert.Equal(15.0, ergebnis.Zeit!.Value.TotalSeconds, 3);
        Assert.Equal(VideoMeterQuelle.Stuetzstellen, ergebnis.Quelle);
    }

    [Fact]
    public void Ein_Meterwert_genau_auf_einer_Stuetzstelle_liefert_deren_Zeit()
    {
        var ergebnis = VideoSprungRechnung.Plane(
            Eingang(stellen: new[] { Punkt(10, 5.0), Punkt(20, 15.0) }),
            Auftrag(meter: 15.0));

        Assert.Equal(20.0, ergebnis.Zeit!.Value.TotalSeconds, 3);
    }

    [Fact]
    public void Steht_die_Kamera_still_gilt_die_fruehere_Zeit()
    {
        // Zwischen Sekunde 20 und 40 bleibt der Meterstand gleich (Kamera haelt an).
        // Der Klick auf 15 m soll an den Anfang dieses Halts fuehren, nicht ans Ende.
        var ergebnis = VideoSprungRechnung.Plane(
            Eingang(stellen: new[] { Punkt(10, 5.0), Punkt(20, 15.0), Punkt(40, 15.0), Punkt(50, 25.0) }),
            Auftrag(meter: 15.0));

        Assert.Equal(20.0, ergebnis.Zeit!.Value.TotalSeconds, 3);
    }

    [Fact]
    public void Vor_der_ersten_Stuetzstelle_zaehlt_die_mittlere_Geschwindigkeit()
    {
        // Erste Stuetzstelle: Sekunde 10 bei 5 m. Bei 0,5 m/s liegen 3 m vier
        // Sekunden davor, also Sekunde 6.
        var ergebnis = VideoSprungRechnung.Plane(
            Eingang(stellen: new[] { Punkt(10, 5.0), Punkt(20, 15.0) }),
            Auftrag(meter: 3.0));

        Assert.Equal(6.0, ergebnis.Zeit!.Value.TotalSeconds, 3);
    }

    [Fact]
    public void Hinter_der_letzten_Stuetzstelle_zaehlt_die_mittlere_Geschwindigkeit()
    {
        // Letzte Stuetzstelle: Sekunde 20 bei 15 m. 20 m sind 5 m weiter,
        // bei 0,5 m/s also 10 s spaeter: Sekunde 30.
        var ergebnis = VideoSprungRechnung.Plane(
            Eingang(stellen: new[] { Punkt(10, 5.0), Punkt(20, 15.0) }),
            Auftrag(meter: 20.0));

        Assert.Equal(30.0, ergebnis.Zeit!.Value.TotalSeconds, 3);
    }

    [Fact]
    public void Ohne_Laenge_und_ohne_Stuetzstellen_wird_nicht_geraten()
    {
        var ergebnis = VideoSprungRechnung.Plane(
            Eingang(laenge: null),
            Auftrag(meter: 25.0));

        Assert.False(ergebnis.Bereit);
        Assert.Equal(VideoSprungGrund.NichtBestimmbar, ergebnis.Grund);
    }

    [Fact]
    public void Ein_negativer_Meterwert_wird_abgewiesen()
    {
        var ergebnis = VideoSprungRechnung.Plane(Eingang(), Auftrag(meter: -1.0));

        Assert.False(ergebnis.Bereit);
        Assert.Equal(VideoSprungGrund.UngueltigerMeter, ergebnis.Grund);
    }

    [Fact]
    public void Ein_unsinniger_Meterwert_wird_abgewiesen()
    {
        Assert.Equal(
            VideoSprungGrund.UngueltigerMeter,
            VideoSprungRechnung.Plane(Eingang(), Auftrag(meter: double.NaN)).Grund);
    }

    [Fact]
    public void Die_Zielzeit_bleibt_innerhalb_des_Videos()
    {
        // 200 m waeren bei 0,5 m/s Sekunde 400 — das Video dauert aber nur 100 s.
        var ergebnis = VideoSprungRechnung.Plane(Eingang(), Auftrag(meter: 200.0));

        Assert.True(ergebnis.Bereit);
        Assert.Equal(100.0, ergebnis.Zeit!.Value.TotalSeconds, 3);
    }

    [Fact]
    public void Die_Zielzeit_wird_nie_negativ()
    {
        // Die erste Stuetzstelle liegt bei Sekunde 2 und 30 m: 0 m laegen rechnerisch
        // weit vor dem Videoanfang.
        var ergebnis = VideoSprungRechnung.Plane(
            Eingang(stellen: new[] { Punkt(2, 30.0) }),
            Auftrag(meter: 0.0));

        Assert.True(ergebnis.Bereit);
        Assert.True(ergebnis.Zeit!.Value >= TimeSpan.Zero);
    }
}
