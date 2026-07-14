using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>Kompatibilitaetsfassade; die Dateiarbeit liegt im Instanzdienst.</summary>
public static class FrameStore
{
    private static ITrainingFrameStore _current = new TrainingFrameFileStore();

    public static ITrainingFrameStore Current => Volatile.Read(ref _current);

    public static void Use(ITrainingFrameStore store) =>
        Volatile.Write(ref _current, store ?? throw new ArgumentNullException(nameof(store)));

    public static string GetFramesDir(string? customDir = null) =>
        Current.GetFramesDir(customDir);

    public static Task<string?> ExtractAndStoreAsync(
        string ffmpegPath,
        string videoPath,
        double timeSeconds,
        string sampleId,
        string? framesDir = null,
        CancellationToken ct = default) =>
        Current.ExtractAndStoreAsync(
            ffmpegPath,
            videoPath,
            timeSeconds,
            sampleId,
            framesDir,
            ct);

    public static string SanitizeFileStem(string sampleId) =>
        TrainingFrameFileName.Sanitize(sampleId);
}
