// AuswertungPro – KI Videoanalyse Modul
using System;
using System.IO;


namespace AuswertungPro.Next.UI.Ai.Shared;

/// <summary>
/// Lokalisiert ffmpeg und ffprobe – entweder über absoluten Pfad (Env-Variable),
/// bekannte Installationsverzeichnisse (WinGet, Chocolatey, Scoop), oder System-PATH.
/// </summary>
public static class FfmpegLocator
{
    /// <summary>Name der Umgebungsvariable für den ffmpeg-Pfad.</summary>
    public const string EnvKey = "SEWERSTUDIO_FFMPEG";
    private const string EnvFfmpeg = EnvKey;

    // Cache damit nicht bei jedem Aufruf gesucht wird
    private static string? _cachedFfmpegPath;

    /// <summary>
    /// Gibt den aufzulösenden ffmpeg-Pfad zurück.
    /// Reihenfolge: ENV → bekannte Installationspfade → "ffmpeg" (PATH).
    /// </summary>
    public static string ResolveFfmpeg()
    {
        var env = Environment.GetEnvironmentVariable(EnvFfmpeg)?.Trim();
        if (!string.IsNullOrEmpty(env))
            return env;

        if (_cachedFfmpegPath is not null)
            return _cachedFfmpegPath;

        var found = FindFfmpegInKnownLocations();
        _cachedFfmpegPath = found ?? "ffmpeg";
        return _cachedFfmpegPath;
    }

    /// <summary>
    /// Durchsucht bekannte Installationsverzeichnisse (WinGet, Chocolatey, Scoop).
    /// </summary>
    private static string? FindFfmpegInKnownLocations()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // WinGet: %LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg*\*\bin\ffmpeg.exe
        var wingetDir = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(wingetDir))
        {
            try
            {
                var ffmpegDirs = Directory.GetDirectories(wingetDir, "Gyan.FFmpeg*");
                foreach (var dir in ffmpegDirs)
                {
                    // Suche rekursiv nach ffmpeg.exe im bin-Verzeichnis
                    var binDirs = Directory.GetDirectories(dir, "bin", SearchOption.AllDirectories);
                    foreach (var bin in binDirs)
                    {
                        var candidate = Path.Combine(bin, "ffmpeg.exe");
                        if (File.Exists(candidate))
                            return candidate;
                    }
                }
            }
            catch { /* Zugriffsfehler ignorieren */ }
        }

        // Chocolatey: C:\ProgramData\chocolatey\bin\ffmpeg.exe
        var chocoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "chocolatey", "bin", "ffmpeg.exe");
        if (File.Exists(chocoPath))
            return chocoPath;

        // Scoop: %USERPROFILE%\scoop\shims\ffmpeg.exe
        var scoopPath = Path.Combine(userProfile, "scoop", "shims", "ffmpeg.exe");
        if (File.Exists(scoopPath))
            return scoopPath;

        // Manuell: C:\ffmpeg\bin\ffmpeg.exe (haeufige manuelle Installation)
        var manualPath = @"C:\ffmpeg\bin\ffmpeg.exe";
        if (File.Exists(manualPath))
            return manualPath;

        // Program Files
        var pfPath = Path.Combine(programFiles, "ffmpeg", "bin", "ffmpeg.exe");
        if (File.Exists(pfPath))
            return pfPath;

        return null;
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
