using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Settings;

/// <summary>
/// Verschiebt eine nicht lesbare Settings-Datei best effort in Quarantaene.
/// </summary>
public sealed class SettingsQuarantineStore : ISettingsQuarantineStore
{
    private readonly object _sync = new();

    public string BuildQuarantinePath(string appDataDirectory, DateTime utcNow)
    {
        var stamp = utcNow.ToString("yyyyMMdd-HHmmssfff");
        return Path.Combine(
            appDataDirectory,
            $"settings.corrupt-{stamp}.json");
    }

    public void TryMoveToQuarantine(
        string settingsPath,
        string appDataDirectory,
        Exception originalException,
        Action<string, Exception?> logAction)
    {
        lock (_sync)
        {
            TryMoveToQuarantineCore(
                settingsPath,
                appDataDirectory,
                originalException,
                logAction);
        }
    }

    private void TryMoveToQuarantineCore(
        string settingsPath,
        string appDataDirectory,
        Exception originalException,
        Action<string, Exception?> logAction)
    {
        string? quarantinePath = null;

        try
        {
            if (!File.Exists(settingsPath))
            {
                logAction(
                    "Settings-Load meldete korrupte Daten, aber settings.json wurde nicht gefunden.",
                    originalException);
                return;
            }

            Directory.CreateDirectory(appDataDirectory);
            quarantinePath = BuildQuarantinePath(
                appDataDirectory,
                DateTime.UtcNow);

            File.Move(settingsPath, quarantinePath, overwrite: false);
            logAction(
                $"Korrupte settings.json wurde nach '{quarantinePath}' verschoben.",
                originalException);
        }
        catch (Exception moveException)
        {
            TryCopyAfterMoveFailure(
                settingsPath,
                appDataDirectory,
                quarantinePath,
                originalException,
                moveException,
                logAction);
        }
    }

    private void TryCopyAfterMoveFailure(
        string settingsPath,
        string appDataDirectory,
        string? quarantinePath,
        Exception originalException,
        Exception moveException,
        Action<string, Exception?> logAction)
    {
        try
        {
            if (!File.Exists(settingsPath))
                return;

            quarantinePath ??= BuildQuarantinePath(
                appDataDirectory,
                DateTime.UtcNow);
            File.Copy(settingsPath, quarantinePath, overwrite: false);

            try
            {
                File.Delete(settingsPath);
            }
            catch
            {
                // Best effort: Die App startet auch dann mit Standardwerten weiter.
            }

            logAction(
                $"Korrupte settings.json wurde nach fehlgeschlagenem Move nach '{quarantinePath}' kopiert.",
                new AggregateException(originalException, moveException));
        }
        catch (Exception copyException)
        {
            logAction(
                "Korrupte settings.json konnte nicht in Quarantaene verschoben werden. Es werden Standardwerte verwendet.",
                new AggregateException(
                    originalException,
                    moveException,
                    copyException));
        }
    }
}
