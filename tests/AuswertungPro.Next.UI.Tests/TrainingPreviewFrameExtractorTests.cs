using System.IO;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingPreviewFrameExtractorTests
{
    [Fact]
    public async Task ExtractPreviewFrameAsync_gibt_null_zurueck_wenn_video_fehlt()
    {
        var settings = new AiRuntimeSettings(
            Enabled: false,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision",
            TextModel: "text",
            EmbedModel: null,
            FfmpegPath: "ffmpeg",
            OllamaRequestTimeout: TimeSpan.FromSeconds(1),
            OllamaKeepAlive: "5m",
            OllamaNumCtx: 2048);

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
}
