using System.IO;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingFfmpegPathResolver
{
    public static string Resolve(string? ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            return "ffmpeg";
        }

        return File.Exists(ffmpegPath) ||
               string.Equals(ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase)
            ? ffmpegPath
            : "ffmpeg";
    }
}
