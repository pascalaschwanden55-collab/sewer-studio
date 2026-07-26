using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// F5: Abschlussbewertung von <see cref="VideoFrameStream"/> — ein Teilvideo (fruehes
/// EOF oder ffmpeg-Fehler-Exit) darf nicht mehr still als Erfolg durchgehen. Getestet
/// ueber die prozessfreie Bewertungsfunktion <see cref="VideoFrameStream.EvaluateCompletion"/>
/// (ExitCode und stderr-Tail werden von <see cref="VideoFrameStream"/> selbst erhoben).
/// </summary>
public sealed class VideoFrameStreamCompletionTests
{
    [Fact]
    public void EvaluateCompletion_SauberesEnde_istComplete()
    {
        var completion = VideoFrameStream.EvaluateCompletion(
            framesRead: 10, expectedFrames: 10, exitCode: 0, stderrTail: "");

        Assert.True(completion.IsComplete);
        Assert.Null(completion.Reason);
        Assert.Equal(10, completion.FramesRead);
        Assert.Equal(10, completion.ExpectedFrames);
    }

    [Fact]
    public void EvaluateCompletion_EinFrameWeniger_liegtInDerToleranz()
    {
        // Der fps-Filter kann je nach Dauer/Rundung einen Frame weniger liefern —
        // dokumentierte Toleranz, kein Teilverlust.
        var completion = VideoFrameStream.EvaluateCompletion(
            framesRead: 9, expectedFrames: 10, exitCode: 0, stderrTail: null);

        Assert.True(completion.IsComplete);
    }

    [Fact]
    public void EvaluateCompletion_FruehesEof_istPartialMitFramezahlen()
    {
        var completion = VideoFrameStream.EvaluateCompletion(
            framesRead: 4, expectedFrames: 10, exitCode: 0, stderrTail: null);

        Assert.False(completion.IsComplete);
        Assert.NotNull(completion.Reason);
        Assert.Contains("fruehes EOF", completion.Reason);
        Assert.Contains("4 von 10", completion.Reason);
    }

    [Fact]
    public void EvaluateCompletion_ExitCodeUngleichNull_GrundEnthaeltStderrAuszug()
    {
        var stderr = "[h264 @ 000001] error while decoding MB 12 34\r\nConversion failed!\r\n";

        var completion = VideoFrameStream.EvaluateCompletion(
            framesRead: 10, expectedFrames: 10, exitCode: 1, stderrTail: stderr);

        Assert.False(completion.IsComplete);
        Assert.NotNull(completion.Reason);
        Assert.Contains("ffmpeg-Exit 1", completion.Reason);
        Assert.Contains("Conversion failed!", completion.Reason);
    }

    [Fact]
    public void EvaluateCompletion_ExitCodeUngleichNull_OhneStderr_GrundNenntNurExit()
    {
        var completion = VideoFrameStream.EvaluateCompletion(
            framesRead: 3, expectedFrames: 10, exitCode: 255, stderrTail: "   ");

        Assert.False(completion.IsComplete);
        Assert.Equal("ffmpeg-Exit 255", completion.Reason);
    }

    [Fact]
    public void EvaluateCompletion_FehlendeFramesUndExitUnbekannt_istPartialMitUnbekannt()
    {
        var completion = VideoFrameStream.EvaluateCompletion(
            framesRead: 3, expectedFrames: 10, exitCode: null, stderrTail: null);

        Assert.False(completion.IsComplete);
        Assert.Contains("unbekannt", completion.Reason);
    }

    [Fact]
    public void EvaluateCompletion_LangerStderr_wirdAufDasEndeBegrenzt()
    {
        var anfang = new string('x', 500);
        var ende = "Fatal: moov atom not found";
        var stderr = anfang + ende;

        var completion = VideoFrameStream.EvaluateCompletion(
            framesRead: 2, expectedFrames: 10, exitCode: 1, stderrTail: stderr);

        Assert.False(completion.IsComplete);
        Assert.NotNull(completion.Reason);
        Assert.Contains(ende, completion.Reason);
        Assert.True(completion.Reason!.Length < stderr.Length,
            $"Reason sollte gekuerzt sein, war aber {completion.Reason.Length} Zeichen lang.");
    }

    [Fact]
    public void ExpectedFrameCount_EntsprichtDerAufruferRechnung()
    {
        Assert.Equal(10, VideoFrameStream.ExpectedFrameCount(30.0, 3.0));
        Assert.Equal(10, VideoFrameStream.ExpectedFrameCount(29.9, 3.0));
        Assert.Equal(0, VideoFrameStream.ExpectedFrameCount(0, 3.0));
        Assert.Equal(0, VideoFrameStream.ExpectedFrameCount(30.0, 0));
    }
}
