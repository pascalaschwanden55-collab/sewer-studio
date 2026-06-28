using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests für FfmpegDurationParser.Parse.
/// Stellt sicher, dass das IST-Verhalten beim Extrahieren aus GetDurationAsync erhalten bleibt.
/// </summary>
public sealed class FfmpegDurationParserTests
{
    [Fact]
    public void Parse_GibtNull_BeiLeererEingabe()
    {
        Assert.Equal(0, FfmpegDurationParser.Parse(""));
        Assert.Equal(0, FfmpegDurationParser.Parse(null!));
    }

    [Fact]
    public void Parse_GibtNull_WennKeinDurationMuster()
    {
        const string stderr = "ffmpeg version 6.0\nInput #0, mov, from 'test.mp4'";
        Assert.Equal(0, FfmpegDurationParser.Parse(stderr));
    }

    [Fact]
    public void Parse_GibtSekunden_FuerEinfachesHHMMSS()
    {
        // 00:01:30 = 90 Sekunden
        const string stderr = "  Duration: 00:01:30.00, start: 0.000000, bitrate: 1234 kb/s";
        Assert.Equal(90.0, FfmpegDurationParser.Parse(stderr), precision: 3);
    }

    [Fact]
    public void Parse_GibtSekunden_MitStundenAnteil()
    {
        // 01:02:03.5 = 1*3600 + 2*60 + 3.5 = 3600 + 120 + 3.5 = 3723.5 Sekunden
        const string stderr = "  Duration: 01:02:03.5, start: 0.000000, bitrate: 2048 kb/s";
        Assert.Equal(3723.5, FfmpegDurationParser.Parse(stderr), precision: 3);
    }

    [Fact]
    public void Parse_GibtSekunden_MitNachkommastellen()
    {
        // 00:00:45.123 = 45.123 Sekunden
        const string stderr = "  Duration: 00:00:45.123, start: 0.000000, bitrate: 500 kb/s";
        Assert.Equal(45.123, FfmpegDurationParser.Parse(stderr), precision: 3);
    }

    [Fact]
    public void Parse_GibtSekunden_WennDurationImMehrzeiligenText()
    {
        // Echte ffmpeg-Stderr enthält viele Zeilen
        const string stderr = """
            ffmpeg version 6.0 Copyright (c) 2000-2023
            Input #0, avi, from 'kanal.avi':
              Metadata:
                encoder: Lavf58
              Duration: 00:25:00.00, start: 0.000000, bitrate: 2048 kb/s
                Stream #0:0: Video: mjpeg
            """;
        // 25 Minuten = 1500 Sekunden
        Assert.Equal(1500.0, FfmpegDurationParser.Parse(stderr), precision: 3);
    }

    [Fact]
    public void Parse_GibtNull_WennDurationNullNull()
    {
        // Manchmal liefert ffmpeg "N/A" oder ähnliches - kein Match
        const string stderr = "  Duration: N/A, start: 0.000000, bitrate: N/A";
        Assert.Equal(0, FfmpegDurationParser.Parse(stderr));
    }
}
