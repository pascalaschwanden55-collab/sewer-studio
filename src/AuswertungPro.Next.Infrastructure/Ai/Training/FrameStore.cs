using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>Kompatibilitaetsfassade; die Dateiarbeit liegt im Instanzdienst.</summary>
public static class FrameStore
{
    private static readonly ITrainingFrameStore Default = new TrainingFrameFileStore();

    public static ITrainingFrameStore Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(ITrainingFrameStore store) =>
        throw new NotSupportedException(
            "Der globale Trainings-Frame-Speicher kann nicht mehr ausgetauscht werden. " +
            "ITrainingFrameStore bitte per Konstruktor uebergeben.");

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
