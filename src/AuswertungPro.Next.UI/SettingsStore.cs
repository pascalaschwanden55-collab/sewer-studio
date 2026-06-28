using System;
using System.IO;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Kapselt das atomare Schreiben der Settings-Datei auf Disk.
/// Erzeugt optional einen Restore-Point und nutzt File.Replace fuer Atomizitaet.
/// </summary>
internal static class SettingsStore
{
    /// <summary>
    /// Speichert den serialisierten Settings-Inhalt atomar an <paramref name="settingsPath"/>.
    /// Schreibt zuerst in eine Temp-Datei, dann atomares Replace (oder Copy+Move als Fallback).
    /// </summary>
    /// <param name="json">Serialisierter Settings-Inhalt.</param>
    /// <param name="settingsPath">Ziel-Pfad der Settings-Datei.</param>
    /// <param name="appDataDir">App-Daten-Verzeichnis; wird erstellt, falls nicht vorhanden.</param>
    /// <param name="enableRestorePoints">Wenn true, wird vor dem Schreiben ein Restore-Point angelegt.</param>
    internal static void Persist(string json, string settingsPath, string appDataDir, bool enableRestorePoints)
    {
        string? tempPath = null;

        try
        {
            Directory.CreateDirectory(appDataDir);

            if (enableRestorePoints)
            {
                RestorePointService.TryCreate(
                    sourceFilePath: settingsPath,
                    restoreRoot: RestorePointService.SettingsRestoreRoot,
                    scopeName: "settings");
            }

            tempPath = Path.Combine(appDataDir, $".{Path.GetFileName(settingsPath)}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempPath, json);

            if (File.Exists(settingsPath))
            {
                var backupPath = settingsPath + ".bak";
                try
                {
                    File.Replace(tempPath, settingsPath, backupPath, ignoreMetadataErrors: true);
                }
                catch (Exception ex) when (ex is PlatformNotSupportedException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    File.Copy(settingsPath, backupPath, overwrite: true);
                    File.Move(tempPath, settingsPath, overwrite: true);
                }
            }
            else
            {
                File.Move(tempPath, settingsPath, overwrite: false);
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* Best-effort-Cleanup */ }
            }
        }
    }
}
