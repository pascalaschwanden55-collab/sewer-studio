using System.IO;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingPreviewFrameExtractorTests
{
    [Fact]
    public async Task ExtractPreviewFrameAsync_verwendet_ffmpeg_und_bereinigte_fall_id()
    {
        var videoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        await File.WriteAllBytesAsync(videoPath, [1]);
        var store = new CapturingTrainingFrameStore("frame.png");

        try
        {
            var service = new TrainingPreviewFrameExtractionService(store);
            var result = await service.ExtractPreviewFrameAsync(
                new TrainingCase
                {
                    CaseId = "Fall 1/2",
                    VideoPath = videoPath
                },
                CreateSettings("C:\\Tools\\ffmpeg.exe"),
                CancellationToken.None);

            Assert.Equal("frame.png", result);
            Assert.Equal("C:\\Tools\\ffmpeg.exe", store.FfmpegPath);
            Assert.Equal(videoPath, store.VideoPath);
            Assert.Equal(2.0, store.TimeSeconds);
            Assert.Equal("preview_Fall_1_2", store.SampleId);
        }
        finally
        {
            File.Delete(videoPath);
        }
    }

    [Fact]
    public async Task ExtractPreviewFrameAsync_gibt_null_zurueck_wenn_video_fehlt()
    {
        var settings = CreateSettings("ffmpeg");

        var result = await TrainingPreviewFrameExtractor.ExtractPreviewFrameAsync(
            new TrainingCase
            {
                CaseId = "case 1",
                VideoPath = Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N") + ".mp4")
            },
            settings,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractPreviewFrameAsync_gibt_bei_Framefehler_null_zurueck()
    {
        var service = new TrainingPreviewFrameExtractionService(
            new ThrowingTrainingFrameStore(),
            _ => true);

        var result = await service.ExtractPreviewFrameAsync(
            new TrainingCase { CaseId = "case", VideoPath = "video.mp4" },
            CreateSettings("ffmpeg"),
            CancellationToken.None);

        Assert.Null(result);
    }

    private static AiRuntimeSettings CreateSettings(string ffmpegPath) =>
        new(
            Enabled: false,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision",
            TextModel: "text",
            EmbedModel: null,
            FfmpegPath: ffmpegPath,
            OllamaRequestTimeout: TimeSpan.FromSeconds(1),
            OllamaKeepAlive: "5m",
            OllamaNumCtx: 2048);

    private sealed class CapturingTrainingFrameStore(string result) : ITrainingFrameStore
    {
        public string? FfmpegPath { get; private set; }
        public string? VideoPath { get; private set; }
        public double TimeSeconds { get; private set; }
        public string? SampleId { get; private set; }

        public string GetFramesDir(string? customDir = null) => throw new NotSupportedException();

        public Task<string?> ExtractAndStoreAsync(
            string ffmpegPath,
            string videoPath,
            double timeSeconds,
            string sampleId,
            string? framesDir = null,
            CancellationToken ct = default)
        {
            FfmpegPath = ffmpegPath;
            VideoPath = videoPath;
            TimeSeconds = timeSeconds;
            SampleId = sampleId;
            return Task.FromResult<string?>(result);
        }
    }

    private sealed class ThrowingTrainingFrameStore : ITrainingFrameStore
    {
        public string GetFramesDir(string? customDir = null) => throw new NotSupportedException();

        public Task<string?> ExtractAndStoreAsync(
            string ffmpegPath,
            string videoPath,
            double timeSeconds,
            string sampleId,
            string? framesDir = null,
            CancellationToken ct = default) =>
            throw new IOException("Testfehler");
    }
}
