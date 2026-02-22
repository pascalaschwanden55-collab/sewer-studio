using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Verwaltet extrahierte Video-Frames im AppData-Ordner.
/// </summary>
public static class FrameStore
{
    public static string GetFramesDir(string? customDir = null)
    {
        if (!string.IsNullOrWhiteSpace(customDir))
        {
            Directory.CreateDirectory(customDir);
            return customDir;
        }
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AuswertungPro", "frames");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Extrahiert einen Frame und speichert ihn als PNG in <framesDir>.
    /// Gibt den gespeicherten Pfad zurück oder null bei Fehler.
    /// </summary>
    public static async Task<string?> ExtractAndStoreAsync(
        string ffmpegPath,
        string videoPath,
        double timeSeconds,
        string sampleId,
        string? framesDir = null,
        CancellationToken ct = default)
    {
        var dir = GetFramesDir(framesDir);
        var outPath = Path.Combine(dir, $"{sampleId}.png");

        if (File.Exists(outPath))
            return outPath;

        var bytes = await VideoFrameExtractor.TryExtractFramePngAsync(
            ffmpegPath, videoPath, TimeSpan.FromSeconds(timeSeconds), ct)
            .ConfigureAwait(false);

        if (bytes is null || bytes.Length == 0)
            return null;

        await File.WriteAllBytesAsync(outPath, bytes, ct).ConfigureAwait(false);
        return outPath;
    }
}
