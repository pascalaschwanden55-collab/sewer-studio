using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

public sealed class TrainingFfmpegFilePathResolver : ITrainingFfmpegPathResolver
{
    public string Resolve(string? ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath))
            return "ffmpeg";

        return File.Exists(ffmpegPath)
               || string.Equals(ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase)
            ? ffmpegPath
            : "ffmpeg";
    }
}
