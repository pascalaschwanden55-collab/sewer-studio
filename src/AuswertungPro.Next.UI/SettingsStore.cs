using System;
using System.IO;
using System.Threading;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Kapselt das atomare Schreiben der Settings-Datei auf Disk.
/// Erzeugt optional einen Restore-Point und nutzt File.Replace fuer Atomizitaet.
/// Kurzzeitige Sperren (Virenscanner, zweiter Prozess) werden per Retry
/// ueberbrueckt, ein Schreibschutz auf der Zieldatei wird aufgehoben —
/// ein stiller Fehlschlag hier kostet sonst die Projekt-Merkliste.
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
    /// <param name="maxAttempts">Schreibversuche insgesamt (Retry bei Sperr-/Zugriffsfehlern).</param>
    /// <param name="retryDelayMs">Wartezeit zwischen den Versuchen.</param>
    internal static void Persist(
        string json,
        string settingsPath,
        string appDataDir,
        bool enableRestorePoints,
        int maxAttempts = 3,
        int retryDelayMs = 200)
    {
        Directory.CreateDirectory(appDataDir);

        if (enableRestorePoints)
        {
            RestorePointService.TryCreate(
                sourceFilePath: settingsPath,
                restoreRoot: RestorePointService.SettingsRestoreRoot,
                scopeName: "settings");
        }

        for (var versuch = 1; ; versuch++)
        {
            try
            {
                PersistOnce(json, settingsPath, appDataDir);
                return;
            }
            catch (Exception ex) when (
                versuch < maxAttempts &&
                (ex is IOException || ex is UnauthorizedAccessException))
            {
                // Kurzzeitige Sperre (Virenscanner, Sync-Tool, zweiter Prozess) — erneut versuchen.
                Thread.Sleep(retryDelayMs);
            }
        }
    }

    private static void PersistOnce(string json, string settingsPath, string appDataDir)
    {
        string? tempPath = null;

        try
        {
            tempPath = Path.Combine(appDataDir, $".{Path.GetFileName(settingsPath)}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempPath, json);

            if (File.Exists(settingsPath))
            {
                // Schreibschutz (z.B. durch Backup-/Sync-Tools gesetzt) wuerde jeden
                // Replace/Move mit UnauthorizedAccessException scheitern lassen.
                TryClearReadOnly(settingsPath);

                var backupPath = settingsPath + ".bak";
                TryClearReadOnly(backupPath);

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

    private static void TryClearReadOnly(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;

            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
        catch
        {
            // Nur Vorbereitung — der eigentliche Schreibversuch meldet den Fehler.
        }
    }
}
