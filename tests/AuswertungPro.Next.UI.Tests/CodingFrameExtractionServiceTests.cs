using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingFrameExtractionServiceTests
{
    [Theory]
    [InlineData(null, 1.0)]
    [InlineData("", 1.0)]
    [InlineData("video.mp4", null)]
    [InlineData("video.mp4", -1.0)]
    public void TryExtractFrameAtSeconds_returns_null_for_invalid_input(string? videoPath, double? seconds)
    {
        var extractorCalled = false;
        var service = new CodingFrameExtractionService(
            () => "ffmpeg",
            (_, _, _, _) =>
            {
                extractorCalled = true;
                return Task.FromResult<byte[]?>(new byte[] { 1 });
            });

        var result = service.TryExtractFrameAtSeconds(videoPath, seconds);

        Assert.Null(result);
        Assert.False(extractorCalled);
    }

    [Fact]
    public void TryExtractFrameAtSeconds_returns_null_when_ffmpeg_is_missing()
    {
        var service = new CodingFrameExtractionService(
            () => "",
            (_, _, _, _) => throw new InvalidOperationException("Extractor must not run."));

        var result = service.TryExtractFrameAtSeconds("video.mp4", 2.5);

        Assert.Null(result);
    }

    [Fact]
    public void TryExtractFrameAtSeconds_calls_extractor_with_resolved_values()
    {
        string? capturedFfmpeg = null;
        string? capturedVideo = null;
        TimeSpan capturedTime = default;
        CancellationToken capturedToken = default;
        var service = new CodingFrameExtractionService(
            () => "ffmpeg.exe",
            (ffmpeg, video, at, ct) =>
            {
                capturedFfmpeg = ffmpeg;
                capturedVideo = video;
                capturedTime = at;
                capturedToken = ct;
                return Task.FromResult<byte[]?>(new byte[] { 4, 5, 6 });
            });

        var result = service.TryExtractFrameAtSeconds("video.mp4", 2.5);

        Assert.Equal(new byte[] { 4, 5, 6 }, result);
        Assert.Equal("ffmpeg.exe", capturedFfmpeg);
        Assert.Equal("video.mp4", capturedVideo);
        Assert.Equal(TimeSpan.FromSeconds(2.5), capturedTime);
        Assert.Equal(CancellationToken.None, capturedToken);
    }

    [Fact]
    public void TryExtractFrameAtSeconds_logs_and_returns_null_when_extractor_fails()
    {
        var logs = new List<string>();
        var service = new CodingFrameExtractionService(
            () => "ffmpeg",
            (_, _, _, _) => throw new InvalidOperationException("kaputt"),
            logs.Add);

        var result = service.TryExtractFrameAtSeconds("video.mp4", 1.0);

        Assert.Null(result);
        var log = Assert.Single(logs);
        Assert.Contains("kaputt", log);
    }
}
