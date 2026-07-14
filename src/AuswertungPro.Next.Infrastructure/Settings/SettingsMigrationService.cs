using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Settings;

/// <summary>
/// Kopiert Legacy-Einstellungen einmalig an den aktuellen Speicherort.
/// </summary>
public sealed class SettingsMigrationService : ISettingsMigrationService
{
    private readonly object _sync = new();

    public SettingsMigrationResult MigrateLegacyIfNeeded(
        string settingsPath,
        string legacySettingsPath,
        string appDataDirectory)
    {
        lock (_sync)
        {
            return MigrateLegacyIfNeededCore(
                settingsPath,
                legacySettingsPath,
                appDataDirectory);
        }
    }

    private static SettingsMigrationResult MigrateLegacyIfNeededCore(
        string settingsPath,
        string legacySettingsPath,
        string appDataDirectory)
    {
        try
        {
            if (File.Exists(settingsPath))
                return SettingsMigrationResult.NoChange();

            if (!File.Exists(legacySettingsPath))
                return SettingsMigrationResult.NoChange();

            Directory.CreateDirectory(appDataDirectory);
            File.Copy(legacySettingsPath, settingsPath, overwrite: false);
            return SettingsMigrationResult.Success();
        }
        catch (Exception ex)
        {
            return SettingsMigrationResult.Failure(ex);
        }
    }
}
