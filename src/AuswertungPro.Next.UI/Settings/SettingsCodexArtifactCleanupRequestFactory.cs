using System;
using AuswertungPro.Next.Application.Maintenance;

namespace AuswertungPro.Next.UI.Settings;

public static class SettingsCodexArtifactCleanupRequestFactory
{
    public static CodexArtifactCleanupRequest Create(DateTime utcNow)
        => Create(AppContext.BaseDirectory, Environment.CurrentDirectory, utcNow);

    internal static CodexArtifactCleanupRequest Create(
        string appBaseDirectory,
        string currentDirectory,
        DateTime utcNow)
        => new(
            SettingsProgramCleanupRequestFactory.FindProgramRoot(
                appBaseDirectory,
                currentDirectory),
            utcNow.AddDays(-1));
}
