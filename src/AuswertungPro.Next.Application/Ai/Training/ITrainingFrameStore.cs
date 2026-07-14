using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>Speichert aus Videos extrahierte Trainingsframes.</summary>
public interface ITrainingFrameStore
{
    string GetFramesDir(string? customDir = null);

    Task<string?> ExtractAndStoreAsync(
        string ffmpegPath,
        string videoPath,
        double timeSeconds,
        string sampleId,
        string? framesDir = null,
        CancellationToken ct = default);
}

/// <summary>Reine Dateinamenregel ohne Datei- oder Prozesszugriff.</summary>
public static class TrainingFrameFileName
{
    public static string Sanitize(string sampleId) =>
        string.IsNullOrEmpty(sampleId)
            ? "frame"
            : Regex.Replace(sampleId, @"[^\w\-]", "_");
}
