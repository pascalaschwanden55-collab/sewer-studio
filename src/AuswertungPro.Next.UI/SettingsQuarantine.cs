using System;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Settings;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Kapselt die Quarantaene-Logik fuer korrupte Settings-Dateien:
/// Berechnet den Quarantaene-Pfad und verschiebt (oder kopiert) die korrupte Datei dorthin.
/// </summary>
internal static class SettingsQuarantine
{
    private static readonly SettingsQuarantineStore DefaultStoreInstance = new();

    internal static ISettingsQuarantineStore DefaultStore => DefaultStoreInstance;

    /// <summary>
    /// Berechnet den Quarantaene-Zielpfad fuer eine korrupte Settings-Datei.
    /// Reine Funktion — kein I/O.
    /// </summary>
    /// <param name="appDataDir">App-Daten-Verzeichnis.</param>
    /// <param name="utcNow">Zeitstempel fuer den Dateinamen (UTC).</param>
    internal static string BuildQuarantinePath(string appDataDir, DateTime utcNow)
        => DefaultStoreInstance.BuildQuarantinePath(appDataDir, utcNow);

    /// <summary>
    /// Versucht, die korrupte Settings-Datei in Quarantaene zu verschieben.
    /// Schlaegt der Move fehl, wird Copy+Delete versucht.
    /// Alle Fehler werden per <paramref name="logAction"/> protokolliert; Ausnahmen werden nicht nach oben weitergegeben.
    /// </summary>
    /// <param name="settingsPath">Pfad der korrupten Settings-Datei.</param>
    /// <param name="appDataDir">App-Daten-Verzeichnis.</param>
    /// <param name="originalException">Die urspruengliche Deserialisierungs-Ausnahme.</param>
    /// <param name="logAction">Callback fuer Logging (Nachricht, optionale Exception).</param>
    internal static void TryMoveToQuarantine(
        string settingsPath,
        string appDataDir,
        Exception originalException,
        Action<string, Exception?> logAction)
        => DefaultStoreInstance.TryMoveToQuarantine(
            settingsPath,
            appDataDir,
            originalException,
            logAction);
}
