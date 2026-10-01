using System;
using AuswertungPro.Next.Application.Media;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Die Stapelextraktion holt eine ganze Bildfolge in einem ffmpeg-Durchgang. Der
/// bestehende Einzelbild-Weg startet ffmpeg je Bild — fuer zehn Minuten Video
/// waeren das 600 Prozessstarts. Hier liegt die reine Logik: Argumente, Namen und
/// die Abbildung Bildnummer -> Videozeit.
/// </summary>
public sealed class VideoFrameSequenceLayoutTests
{
    [Fact]
    public void Das_erste_Bild_liegt_bei_Sekunde_null()
    {
        // f000001 ist das Bild zum Videoanfang, nicht zu Sekunde 1. Ein Fehler hier
        // verschiebt jeden Vorschlag um eine Sekunde und damit um bis zu 0,1 m.
        Assert.Equal(0.0, VideoFrameSequenceLayout.TimeSecondsFor(1, framesPerSecond: 1.0), 6);
        Assert.Equal(1.0, VideoFrameSequenceLayout.TimeSecondsFor(2, framesPerSecond: 1.0), 6);
        Assert.Equal(29.0, VideoFrameSequenceLayout.TimeSecondsFor(30, framesPerSecond: 1.0), 6);
    }

    [Fact]
    public void Eine_feinere_Abtastung_halbiert_den_Abstand()
    {
        Assert.Equal(0.0, VideoFrameSequenceLayout.TimeSecondsFor(1, 2.0), 6);
        Assert.Equal(0.5, VideoFrameSequenceLayout.TimeSecondsFor(2, 2.0), 6);
        Assert.Equal(1.0, VideoFrameSequenceLayout.TimeSecondsFor(3, 2.0), 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Eine_ungueltige_Bildnummer_wird_abgewiesen(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VideoFrameSequenceLayout.TimeSecondsFor(index, 1.0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Eine_ungueltige_Abtastrate_wird_abgewiesen(double fps)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VideoFrameSequenceLayout.TimeSecondsFor(1, fps));
    }

    [Fact]
    public void Aus_dem_Dateinamen_laesst_sich_die_Bildnummer_zurueckgewinnen()
    {
        Assert.Equal(1, VideoFrameSequenceLayout.TryParseIndex("f000001.jpg"));
        Assert.Equal(1234, VideoFrameSequenceLayout.TryParseIndex("f001234.jpg"));
        Assert.Null(VideoFrameSequenceLayout.TryParseIndex("vorschau.jpg"));
        Assert.Null(VideoFrameSequenceLayout.TryParseIndex("f00abc1.jpg"));
        Assert.Null(VideoFrameSequenceLayout.TryParseIndex(null));
    }

    [Fact]
    public void Die_ffmpeg_Argumente_tasten_gleichmaessig_ab_und_ueberschreiben_nichts()
    {
        var argumente = VideoFrameSequenceLayout.BuildArguments(
            @"D:\Videos\H_1-2.mpg", @"C:\tmp\lauf", framesPerSecond: 1.0);

        Assert.Contains("fps=1", argumente);
        Assert.Contains(@"D:\Videos\H_1-2.mpg", argumente);
        Assert.Contains("f%06d.jpg", argumente);
        // -y wuerde vorhandene Bilder ueberschreiben; der Zielordner muss leer sein.
        Assert.DoesNotContain(" -y ", argumente);
    }

    [Fact]
    public void Die_Abtastrate_erscheint_unabhaengig_von_der_Landessprache()
    {
        // Mit deutscher Kultur wuerde 0,5 statt 0.5 entstehen — ffmpeg versteht das nicht.
        var argumente = VideoFrameSequenceLayout.BuildArguments(
            "video.mpg", "ziel", framesPerSecond: 0.5);

        Assert.Contains("fps=0.5", argumente);
        Assert.DoesNotContain("fps=0,5", argumente);
    }

    [Fact]
    public void Leere_Pfade_werden_abgewiesen()
    {
        Assert.Throws<ArgumentException>(
            () => VideoFrameSequenceLayout.BuildArguments("  ", "ziel", 1.0));
        Assert.Throws<ArgumentException>(
            () => VideoFrameSequenceLayout.BuildArguments("video.mpg", "  ", 1.0));
    }
}
