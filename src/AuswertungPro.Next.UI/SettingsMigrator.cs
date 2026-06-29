using System.IO;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Kapselt die Migrations-Logik fuer Settings-Dateien:
/// Falls die neue Settings-Datei noch nicht existiert, aber eine Legacy-Version vorhanden ist,
/// wird diese an den neuen Pfad kopiert.
/// </summary>
internal static class SettingsMigrator
{
    /// <summary>
    /// Kopiert die Legacy-Settings-Datei an den neuen Pfad, sofern der neue Pfad noch nicht belegt ist.
    /// Fehler werden still ignoriert (Migrations-Fehler sollen den Start nicht blockieren).
    /// </summary>
    /// <param name="settingsPath">Ziel-Pfad der aktuellen Settings-Datei.</param>
    /// <param name="legacySettingsPath">Quell-Pfad der alten Settings-Datei.</param>
    /// <param name="appDataDir">App-Daten-Verzeichnis; wird erstellt, falls nicht vorhanden.</param>
    internal static void MigrateLegacyIfNeeded(string settingsPath, string legacySettingsPath, string appDataDir)
    {
        try
        {
            if (File.Exists(settingsPath))
                return;

            if (!File.Exists(legacySettingsPath))
                return;

            Directory.CreateDirectory(appDataDir);
            File.Copy(legacySettingsPath, settingsPath, overwrite: false);
        }
        catch
        {
            // Migrations-Fehler werden ignoriert.
        }
    }
}
