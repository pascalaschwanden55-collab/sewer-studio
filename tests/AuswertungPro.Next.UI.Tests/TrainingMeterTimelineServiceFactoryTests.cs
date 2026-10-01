using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingMeterTimelineServiceFactoryTests
{
    [Fact]
    public async Task Create_liefert_deaktivierten_meter_service_ohne_osd()
    {
        using var service = TrainingMeterTimelineServiceFactory.Create(RuntimeSettings(enabled: false), concurrency: 4);

        var timeline = await service.BuildTimelineAsync(
            videoPath: "nicht-vorhanden.mp4",
            videoDurationSeconds: 30,
            ct: CancellationToken.None);

        Assert.Empty(timeline);
    }

    [Fact]
    public void Create_validiert_runtime_settings()
    {
        Assert.Throws<ArgumentNullException>(() => TrainingMeterTimelineServiceFactory.Create(null!));
    }

    private static AiRuntimeSettings RuntimeSettings(bool enabled)
        => new(
            Enabled: enabled,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision",
            TextModel: "text",
            EmbedModel: "embed",
            FfmpegPath: "ffmpeg",
            OllamaRequestTimeout: TimeSpan.FromMinutes(2),
            OllamaKeepAlive: "5m",
            OllamaNumCtx: 2048);
}
