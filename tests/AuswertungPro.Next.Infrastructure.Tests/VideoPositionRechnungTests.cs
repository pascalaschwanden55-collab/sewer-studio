using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Video;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Fuehrt Live-Zustand des Players und Stuetzstellen zur Antwort des Endpunkts
/// /qgis/video_position.json zusammen. Ohne belastbaren Meterwert entsteht KEINE
/// Antwort — das Plugin bleibt dann still, statt einen erfundenen Marker zu zeigen.
/// </summary>
public sealed class VideoPositionRechnungTests
{
    private static VideoPositionEingang Eingang(
        string haltung = "80475-80462",
        double? laenge = 50.0,
        IReadOnlyList<VideoMeterStuetzstelle>? stellen = null,
        int sekunde = 50,
        int dauerSekunden = 100)
        => new(
            haltung,
            TimeSpan.FromSeconds(sekunde),
            TimeSpan.FromSeconds(dauerSekunden),
            Playing: true,
            laenge,
            stellen ?? Array.Empty<VideoMeterStuetzstelle>());

    [Fact]
    public void Ohne_Eingang_gibt_es_keine_Antwort()
        => Assert.Null(VideoPositionRechnung.Rechne(null));

    [Fact]
    public void Ohne_Haltungsnamen_gibt_es_keine_Antwort()
        => Assert.Null(VideoPositionRechnung.Rechne(Eingang(haltung: "   ")));

    [Fact]
    public void Ohne_Laenge_und_ohne_Stuetzstellen_gibt_es_keine_Antwort()
        => Assert.Null(VideoPositionRechnung.Rechne(Eingang(laenge: null)));

    [Fact]
    public void Mit_Laenge_entsteht_eine_Grobortung()
    {
        var antwort = VideoPositionRechnung.Rechne(Eingang());

        Assert.NotNull(antwort);
        Assert.Equal("80475-80462", antwort!.Haltung);
        Assert.Equal(25.0, antwort.Meter, 3);
        Assert.Equal(50.0, antwort.Laenge!.Value, 3);
        Assert.True(antwort.Playing);
        Assert.Equal(VideoMeterQuelle.MittlereGeschwindigkeit, antwort.Quelle);
    }

    [Fact]
    public void Mit_Stuetzstellen_wird_die_genauere_Quelle_gemeldet()
    {
        var antwort = VideoPositionRechnung.Rechne(Eingang(
            stellen: new[]
            {
                new VideoMeterStuetzstelle(TimeSpan.FromSeconds(40), 30.0),
                new VideoMeterStuetzstelle(TimeSpan.FromSeconds(60), 40.0)
            }));

        Assert.NotNull(antwort);
        Assert.Equal(35.0, antwort!.Meter, 3);
        Assert.Equal(VideoMeterQuelle.Stuetzstellen, antwort.Quelle);
    }

    [Fact]
    public void Die_Videozeit_wird_lesbar_ausgegeben()
    {
        var antwort = VideoPositionRechnung.Rechne(Eingang(sekunde: 83));

        Assert.Equal("00:01:23.0", antwort!.Zeit);
    }
}
