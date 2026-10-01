using System;
using AuswertungPro.Next.Application.Video;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Rechnet die Videozeit in einen Meterstand der Haltung um (QGIS-Live-Position).
/// Stuetzstellen sind die codierten Beobachtungen: jede traegt Videozeit und Meter.
/// Wo keine Stuetzstelle liegt, gilt die mittlere Vorschubgeschwindigkeit.
/// Ist beides unbekannt, wird NICHT geraten.
/// </summary>
public sealed class VideoMeterSpurTests
{
    private static VideoMeterStuetzstelle Punkt(int sekunde, double meter)
        => new(TimeSpan.FromSeconds(sekunde), meter);

    [Fact]
    public void Zwischen_zwei_Stuetzstellen_wird_linear_interpoliert()
    {
        var spur = VideoMeterSpur.Baue(
            new[] { Punkt(10, 5.0), Punkt(20, 15.0) },
            haltungslaengeM: 50.0,
            videodauer: TimeSpan.FromSeconds(100));

        Assert.Equal(10.0, spur.MeterBei(TimeSpan.FromSeconds(15))!.Value, 3);
        Assert.Equal(VideoMeterQuelle.Stuetzstellen, spur.Quelle);
    }

    [Fact]
    public void Eine_Stuetzstelle_liefert_ihren_eigenen_Meterwert()
    {
        var spur = VideoMeterSpur.Baue(
            new[] { Punkt(10, 5.0), Punkt(20, 15.0) },
            haltungslaengeM: 50.0,
            videodauer: TimeSpan.FromSeconds(100));

        Assert.Equal(15.0, spur.MeterBei(TimeSpan.FromSeconds(20))!.Value, 3);
    }

    [Fact]
    public void Ausserhalb_der_Stuetzstellen_gilt_die_mittlere_Geschwindigkeit()
    {
        // 50 m in 100 s = 0,5 m/s. 10 s nach der letzten Stuetzstelle (15 m) also 20 m.
        var spur = VideoMeterSpur.Baue(
            new[] { Punkt(10, 5.0), Punkt(20, 15.0) },
            haltungslaengeM: 50.0,
            videodauer: TimeSpan.FromSeconds(100));

        Assert.Equal(20.0, spur.MeterBei(TimeSpan.FromSeconds(30))!.Value, 3);
        Assert.Equal(2.5, spur.MeterBei(TimeSpan.FromSeconds(5))!.Value, 3);
    }

    [Fact]
    public void Ohne_Stuetzstellen_zaehlt_nur_Laenge_geteilt_durch_Dauer()
    {
        var spur = VideoMeterSpur.Baue(
            Array.Empty<VideoMeterStuetzstelle>(),
            haltungslaengeM: 50.0,
            videodauer: TimeSpan.FromSeconds(100));

        Assert.Equal(25.0, spur.MeterBei(TimeSpan.FromSeconds(50))!.Value, 3);
        Assert.Equal(VideoMeterQuelle.MittlereGeschwindigkeit, spur.Quelle);
    }

    [Fact]
    public void Ohne_Laenge_und_ohne_Stuetzstellen_wird_nicht_geraten()
    {
        var spur = VideoMeterSpur.Baue(
            Array.Empty<VideoMeterStuetzstelle>(),
            haltungslaengeM: null,
            videodauer: TimeSpan.FromSeconds(100));

        Assert.Null(spur.MeterBei(TimeSpan.FromSeconds(50)));
        Assert.Equal(VideoMeterQuelle.Unbekannt, spur.Quelle);
    }

    [Fact]
    public void Ruecklaeufige_Stuetzstellen_werden_verworfen()
    {
        // Die dritte Stuetzstelle springt zurueck — ein Tippfehler im Protokoll darf
        // den Marker nicht ruecklaufen lassen.
        var spur = VideoMeterSpur.Baue(
            new[] { Punkt(10, 5.0), Punkt(20, 15.0), Punkt(30, 2.0), Punkt(40, 25.0) },
            haltungslaengeM: 50.0,
            videodauer: TimeSpan.FromSeconds(100));

        Assert.Equal(20.0, spur.MeterBei(TimeSpan.FromSeconds(30))!.Value, 3);
    }

    [Fact]
    public void Der_Meterwert_bleibt_innerhalb_der_Haltung()
    {
        // Video laeuft nach dem Rohrende weiter (Nachlauf): der Marker klebt am Ende,
        // statt aus der Haltung herauszuwandern.
        var spur = VideoMeterSpur.Baue(
            Array.Empty<VideoMeterStuetzstelle>(),
            haltungslaengeM: 30.0,
            videodauer: TimeSpan.FromSeconds(60));

        Assert.Equal(30.0, spur.MeterBei(TimeSpan.FromSeconds(90))!.Value, 3);
        Assert.Equal(0.0, spur.MeterBei(TimeSpan.FromSeconds(-5))!.Value, 3);
    }
}
