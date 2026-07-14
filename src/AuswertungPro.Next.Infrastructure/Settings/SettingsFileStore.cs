using System;
using System.IO;
using System.Threading;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Settings;

/// <summary>
/// Schreibt Programmeinstellungen atomar und wiederholt kurzzeitig blockierte Schreibversuche.
/// </summary>
public sealed class SettingsFileStore : ISettingsFileStore
{
    private readonly Action<string> _createRestorePoint;
    private readonly int _maxAttempts;
    private readonly int _retryDelayMs;

    public SettingsFileStore(
        Action<string> createRestorePoint,
        int maxAttempts = 3,
        int retryDelayMs = 200)
    {
        _createRestorePoint = createRestorePoint
            ?? throw new ArgumentNullException(nameof(createRestorePoint));
        _maxAttempts = maxAttempts;
        _retryDelayMs = retryDelayMs;
    }

    public void Persist(
        string json,
        string settingsPath,
        string appDataDirectory,
        bool enableRestorePoints)
    {
        Directory.CreateDirectory(appDataDirectory);

        if (enableRestorePoints)
            _createRestorePoint(settingsPath);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                PersistOnce(json, settingsPath, appDataDirectory);
                return;
            }
            catch (Exception ex) when (
                attempt < _maxAttempts
                && (ex is IOException || ex is UnauthorizedAccessException))
            {
                Thread.Sleep(_retryDelayMs);
            }
        }
    }

    private static void PersistOnce(
        string json,
        string settingsPath,
        string appDataDirectory)
    {
        string? tempPath = null;

        try
        {
            tempPath = Path.Combine(
                appDataDirectory,
                $".{Path.GetFileName(settingsPath)}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempPath, json);

            if (File.Exists(settingsPath))
            {
                TryClearReadOnly(settingsPath);

                var backupPath = settingsPath + ".bak";
                TryClearReadOnly(backupPath);

                try
                {
                    File.Replace(tempPath, settingsPath, backupPath, ignoreMetadataErrors: true);
                }
                catch (Exception ex) when (
                    ex is PlatformNotSupportedException
                    || ex is IOException
                    || ex is UnauthorizedAccessException)
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
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Eine Temp-Datei darf den eigentlichen Speicherfehler nicht verdecken.
                }
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
            // Der eigentliche Schreibversuch meldet einen verbleibenden Fehler.
        }
    }
}
