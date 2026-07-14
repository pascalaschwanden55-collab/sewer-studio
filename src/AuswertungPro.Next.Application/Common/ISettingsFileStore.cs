namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Speichert den bereits serialisierten Inhalt der Programmeinstellungen.
/// </summary>
public interface ISettingsFileStore
{
    void Persist(
        string json,
        string settingsPath,
        string appDataDirectory,
        bool enableRestorePoints);
}
