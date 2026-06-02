using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Verwaltet extrahierte Video-Frames im Knowledge-Ordner.
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
        return KnowledgeBasePaths.GetFramesDir();
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
        var outPath = Path.Combine(dir, $"{SanitizeFileStem(sampleId)}.png");

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

    /// <summary>
    /// Macht aus einem (evtl. pfad-unsicheren) Sample-Bezeichner einen sicheren Datei-Stamm:
    /// alles ausser Wort-Zeichen und '-' wird zu '_'. Verhindert, dass z.B. eine verschachtelte
    /// CaseId mit '/' beim Frame-Schreiben einen nicht existierenden Unterordner adressiert.
    /// </summary>
    public static string SanitizeFileStem(string sampleId)
    {
        if (string.IsNullOrEmpty(sampleId))
            return "frame";

        return Regex.Replace(sampleId, @"[^\w\-]", "_");
    }
}
