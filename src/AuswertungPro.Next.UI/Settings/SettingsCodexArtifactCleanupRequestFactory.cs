using System;
using AuswertungPro.Next.Application.Maintenance;

namespace AuswertungPro.Next.UI.Settings;

public static class SettingsCodexArtifactCleanupRequestFactory
{
    public static CodexArtifactCleanupRequest Create(DateTime utcNow)
        => Create(
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            utcNow,
            SettingsProgramCleanupRequestFactory.CompatibilityService);

    internal static CodexArtifactCleanupRequest Create(
        DateTime utcNow,
        IProgramRootLocator programRootLocator)
        => Create(
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            utcNow,
            programRootLocator);

    internal static CodexArtifactCleanupRequest Create(
        string appBaseDirectory,
        string currentDirectory,
        DateTime utcNow)
        => Create(
            appBaseDirectory,
            currentDirectory,
            utcNow,
            SettingsProgramCleanupRequestFactory.CompatibilityService);

    internal static CodexArtifactCleanupRequest Create(
        string appBaseDirectory,
        string currentDirectory,
        DateTime utcNow,
        IProgramRootLocator programRootLocator)
    {
        ArgumentNullException.ThrowIfNull(programRootLocator);

        return new CodexArtifactCleanupRequest(
            programRootLocator.FindProgramRoot(
                appBaseDirectory,
                currentDirectory),
            utcNow.AddDays(-1));
    }
}
