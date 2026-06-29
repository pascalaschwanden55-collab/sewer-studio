using System;
using System.IO;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Kapselt die Quarantaene-Logik fuer korrupte Settings-Dateien:
/// Berechnet den Quarantaene-Pfad und verschiebt (oder kopiert) die korrupte Datei dorthin.
/// </summary>
internal static class SettingsQuarantine
{
    /// <summary>
    /// Berechnet den Quarantaene-Zielpfad fuer eine korrupte Settings-Datei.
    /// Reine Funktion — kein I/O.
    /// </summary>
    /// <param name="appDataDir">App-Daten-Verzeichnis.</param>
    /// <param name="utcNow">Zeitstempel fuer den Dateinamen (UTC).</param>
    internal static string BuildQuarantinePath(string appDataDir, DateTime utcNow)
    {
        var stamp = utcNow.ToString("yyyyMMdd-HHmmssfff");
        return Path.Combine(appDataDir, $"settings.corrupt-{stamp}.json");
    }

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
    {
        string? quarantinePath = null;

        try
        {
            if (!File.Exists(settingsPath))
            {
                logAction("Settings-Load meldete korrupte Daten, aber settings.json wurde nicht gefunden.", originalException);
                return;
            }

            Directory.CreateDirectory(appDataDir);
            quarantinePath = BuildQuarantinePath(appDataDir, DateTime.UtcNow);

            File.Move(settingsPath, quarantinePath, overwrite: false);
            logAction($"Korrupte settings.json wurde nach '{quarantinePath}' verschoben.", originalException);
        }
        catch (Exception moveEx)
        {
            try
            {
                if (!File.Exists(settingsPath))
                    return;

                quarantinePath ??= BuildQuarantinePath(appDataDir, DateTime.UtcNow);
                File.Copy(settingsPath, quarantinePath, overwrite: false);

                try
                {
                    File.Delete(settingsPath);
                }
                catch
                {
                    // Best-effort-Loeschen; falls fehlgeschlagen, startet die App trotzdem mit Standardwerten.
                }

                logAction(
                    $"Korrupte settings.json wurde nach fehlgeschlagenem Move nach '{quarantinePath}' kopiert.",
                    new AggregateException(originalException, moveEx));
            }
            catch (Exception copyEx)
            {
                logAction(
                    "Korrupte settings.json konnte nicht in Quarantaene verschoben werden. Es werden Standardwerte verwendet.",
                    new AggregateException(originalException, moveEx, copyEx));
            }
        }
    }
}
