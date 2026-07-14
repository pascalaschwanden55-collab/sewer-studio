using System.Threading;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingFfmpegPathResolver
{
    private static ITrainingFfmpegPathResolver _current = new TrainingFfmpegFilePathResolver();

    internal static ITrainingFfmpegPathResolver CompatibilityService
        => Volatile.Read(ref _current);

    internal static void Use(ITrainingFfmpegPathResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        Volatile.Write(ref _current, resolver);
    }

    public static string Resolve(string? ffmpegPath)
        => CompatibilityService.Resolve(ffmpegPath);
}
