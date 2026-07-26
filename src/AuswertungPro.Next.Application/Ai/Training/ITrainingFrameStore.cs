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

    /// <summary>
    /// Kopiert ein vorhandenes Foto unveraendert und inhaltsadressiert in den
    /// Trainingsspeicher. Das Quellbild bleibt unangetastet.
    /// </summary>
    Task<string?> StoreExistingAsync(
        string sourcePath,
        string? framesDir = null,
        CancellationToken ct = default);

    /// <summary>
    /// Speichert bereits vorliegende Bildbytes unveraendert und inhaltsadressiert.
    /// </summary>
    Task<string?> StoreBytesAsync(
        byte[] imageBytes,
        string extension,
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
