using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Settings;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI;

/// <summary>
/// Kompatibilitaetsfassade fuer den instanzbasierten Settings-Dateispeicher.
/// </summary>
internal static class SettingsStore
{
    internal static ISettingsFileStore CreateDefault(
        int maxAttempts = 3,
        int retryDelayMs = 200)
        => new SettingsFileStore(
            sourceFilePath => RestorePointService.TryCreate(
                sourceFilePath,
                RestorePointService.SettingsRestoreRoot,
                "settings"),
            maxAttempts,
            retryDelayMs);

    internal static void Persist(
        string json,
        string settingsPath,
        string appDataDir,
        bool enableRestorePoints,
        int maxAttempts = 3,
        int retryDelayMs = 200)
        => CreateDefault(maxAttempts, retryDelayMs).Persist(
            json,
            settingsPath,
            appDataDir,
            enableRestorePoints);
}
