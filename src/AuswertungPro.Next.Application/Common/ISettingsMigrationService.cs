namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Uebernimmt eine vorhandene Einstellungsdatei aus dem alten Programmordner.
/// </summary>
public interface ISettingsMigrationService
{
    SettingsMigrationResult MigrateLegacyIfNeeded(
        string settingsPath,
        string legacySettingsPath,
        string appDataDirectory);
}

public sealed record SettingsMigrationResult(bool Migrated, Exception? Error)
{
    public static SettingsMigrationResult NoChange() => new(false, null);

    public static SettingsMigrationResult Success() => new(true, null);

    public static SettingsMigrationResult Failure(Exception error) => new(false, error);
}
