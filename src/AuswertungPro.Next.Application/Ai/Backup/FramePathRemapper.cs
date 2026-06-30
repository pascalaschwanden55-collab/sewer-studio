using System;
using System.IO;

namespace AuswertungPro.Next.Application.Ai.Backup;

/// <summary>
/// Kapselt die reine Pfad-Mapping-Logik fuer Frame- und Annotations-Pfade
/// beim Import eines Backup-Archivs auf einen anderen Rechner.
/// Enthaelt kein IO (kein File.Exists/ReadAll) — nur String-Logik.
/// </summary>
public static class FramePathRemapper
{
    /// <summary>
    /// Gibt den lokalen Pfad zurueck, wenn eine Datei im angegebenen Zielordner
    /// gefunden wird und der aktuelle Pfad auf einen anderen Rechner zeigt.
    /// Prueft zuerst direkt in localDir, dann in einem Unterverzeichnis
    /// (z.B. teacher_images/crops/).
    /// </summary>
    /// <param name="path">Vorhandener (ggf. fremder) absoluter Pfad.</param>
    /// <param name="localDir">Lokales Zielverzeichnis.</param>
    /// <param name="fileExists">Delegate zum Pruefen ob eine Datei existiert (Test-freundlich).</param>
    /// <returns>Neuer lokaler Pfad oder null, wenn kein Remap noetig oder moeglich.</returns>
    public static string? RemapPathToLocal(
        string? path,
        string localDir,
        Func<string, bool> fileExists)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var fileName = Path.GetFileName(path);

        // Direkter Pfad: localDir/filename
        var localPath = Path.Combine(localDir, fileName);
        if (fileExists(localPath)
            && !string.Equals(path, localPath, StringComparison.OrdinalIgnoreCase))
        {
            return localPath;
        }

        // Unterverzeichnis beibehalten (z.B. crops/filename)
        var parentDir = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
        if (!string.IsNullOrEmpty(parentDir)
            && !string.Equals(parentDir, Path.GetFileName(localDir), StringComparison.OrdinalIgnoreCase))
        {
            var subPath = Path.Combine(localDir, parentDir, fileName);
            if (fileExists(subPath)
                && !string.Equals(path, subPath, StringComparison.OrdinalIgnoreCase))
            {
                return subPath;
            }
        }

        return null;
    }

    /// <summary>
    /// Prueft ob ein Frame-Pfad auf den lokalen Frame-Ordner remapped werden soll,
    /// und gibt den neuen Pfad zurueck. Gibt null zurueck wenn kein Remap noetig.
    /// </summary>
    /// <param name="framePath">Vorhandener absoluter Frame-Pfad (ggf. fremd).</param>
    /// <param name="localFramesDir">Lokales frames/-Verzeichnis.</param>
    /// <param name="fileExists">Delegate zum Pruefen ob eine Datei existiert.</param>
    public static string? RemapFramePath(
        string? framePath,
        string localFramesDir,
        Func<string, bool> fileExists)
    {
        if (string.IsNullOrEmpty(framePath)) return null;

        var fileName = Path.GetFileName(framePath);
        var localPath = Path.Combine(localFramesDir, fileName);

        if (fileExists(localPath)
            && !string.Equals(framePath, localPath, StringComparison.OrdinalIgnoreCase))
        {
            return localPath;
        }

        return null;
    }
}
