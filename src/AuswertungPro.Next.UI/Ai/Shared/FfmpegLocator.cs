// AuswertungPro – KI Videoanalyse Modul
using System;
using System.IO;

namespace AuswertungPro.Next.UI.Ai.Shared;

/// <summary>
/// Lokalisiert ffmpeg und ffprobe – entweder über absoluten Pfad (Env-Variable),
/// nebeneinander im selben Verzeichnis, oder über den System-PATH.
/// </summary>
public static class FfmpegLocator
{
    /// <summary>Name der Umgebungsvariable für den ffmpeg-Pfad.</summary>
    public const string EnvKey = "AUSWERTUNGPRO_FFMPEG";
    private const string EnvFfmpeg = EnvKey;

    /// <summary>
    /// Gibt den aufzulösenden ffmpeg-Pfad zurück.
    /// Reihenfolge: ENV → absoluter Pfad → "ffmpeg" (PATH).
    /// </summary>
    public static string ResolveFfmpeg()
    {
        var env = Environment.GetEnvironmentVariable(EnvFfmpeg)?.Trim();
        if (!string.IsNullOrEmpty(env))
            return env;
        return "ffmpeg";
    }

    /// <summary>
    /// Gibt den aufzulösenden ffprobe-Pfad zurück.
    /// Sucht ffprobe.exe neben ffmpeg, sonst "ffprobe" (PATH).
    /// </summary>
    public static string ResolveFfprobe()
    {
        var ffmpeg = ResolveFfmpeg();

        // Absoluter Pfad: ffprobe.exe daneben suchen
        if (Path.IsPathRooted(ffmpeg) && File.Exists(ffmpeg))
        {
            var dir = Path.GetDirectoryName(ffmpeg)!;
            var ext = Path.GetExtension(ffmpeg);          // ".exe" auf Windows
            var candidate = Path.Combine(dir, "ffprobe" + ext);
            if (File.Exists(candidate))
                return candidate;
        }

        // Aus dem Namen ableiten (z.B. "ffmpeg" → "ffprobe")
        if (string.Equals(ffmpeg, "ffmpeg", StringComparison.OrdinalIgnoreCase))
            return "ffprobe";

        var derivedDir = Path.GetDirectoryName(ffmpeg);
        var derivedExt = Path.GetExtension(ffmpeg);
        return string.IsNullOrEmpty(derivedDir)
            ? "ffprobe" + derivedExt
            : Path.Combine(derivedDir, "ffprobe" + derivedExt);
    }

    /// <summary>
    /// Prüft, ob ffmpeg über Process.Start erreichbar ist (absolut oder im PATH).
    /// </summary>
    public static bool IsFfmpegAvailable()
    {
        var path = ResolveFfmpeg();
        if (Path.IsPathRooted(path))
            return File.Exists(path);
        // PATH-basiert: Datei kann nicht via File.Exists geprüft werden
        return true;
    }
}
