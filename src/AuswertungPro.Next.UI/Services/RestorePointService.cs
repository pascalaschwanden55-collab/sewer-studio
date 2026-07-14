using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Settings;

namespace AuswertungPro.Next.UI.Services;

public static class RestorePointService
{
    private static readonly ISettingsRestorePointStore DefaultStore =
        new SettingsRestorePointStore();

    public const int MaxRestorePointsPerScope =
        SettingsRestorePointStore.MaxRestorePointsPerScope;

    public static string SettingsRestoreRoot =>
        Path.Combine(AppSettings.AppDataDir, "restore-points");

    public static void TryCreate(string sourceFilePath, string restoreRoot, string scopeName)
        => DefaultStore.TryCreate(sourceFilePath, restoreRoot, scopeName);
}
