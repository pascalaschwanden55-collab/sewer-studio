using System;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Ai.Backup;

/// <summary>
/// Sicherheitspruefungen fuer Verzeichnis-Operationen im Backup-Kontext.
/// Verhindert versehentliches Loeschen oder Beschreiben beliebiger Verzeichnisse.
/// </summary>
public static class SafePathGuard
{
    /// <summary>
    /// Gibt true zurueck wenn der Pfad sicher geloescht werden darf:
    ///   1. Pfad liegt im Temp-Verzeichnis des Betriebssystems.
    ///   2. Pfad enthaelt den Teilstring "backup" (Gross-/Kleinschreibung egal).
    /// Beide Bedingungen muessen gleichzeitig erfuellt sein.
    /// </summary>
    /// <param name="dirPath">Zu pruefender Verzeichnispfad.</param>
    public static bool IsSafeToDelete(string? dirPath)
    {
        if (string.IsNullOrWhiteSpace(dirPath))
            return false;

        var tempRoot = Path.GetTempPath()
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedDir = Path.GetFullPath(dirPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Bedingung 1: Muss im Temp-Verzeichnis liegen
        if (!normalizedDir.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            return false;

        // Bedingung 2: Muss "backup" im Pfad enthalten
        if (!normalizedDir.Contains("backup", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
