using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingFfmpegPathResolver
{
    private static readonly ITrainingFfmpegPathResolver Default =
        new TrainingFfmpegFilePathResolver();

    internal static ITrainingFfmpegPathResolver CompatibilityService
        => Default;

    public static string Resolve(string? ffmpegPath)
        => CompatibilityService.Resolve(ffmpegPath);
}
