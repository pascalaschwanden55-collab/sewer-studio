namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Sichert eine nicht lesbare Settings-Datei in einem Quarantaene-Ordner.
/// </summary>
public interface ISettingsQuarantineStore
{
    void TryMoveToQuarantine(
        string settingsPath,
        string appDataDirectory,
        Exception originalException,
        Action<string, Exception?> logAction);
}
