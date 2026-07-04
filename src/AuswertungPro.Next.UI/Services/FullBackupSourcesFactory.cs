using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.UI.Services;

public static class FullBackupSourcesFactory
{
    public static FullBackupSources ErmittleAktuelleQuellen()
        => new(
            RepoRoot: RepoRootLocator.Locate(),
            KnowledgeRoot: KnowledgeBasePaths.GetRoot(),
            LocalSewerStudioDir: AppSettings.AppDataDir,
            RoamingSewerStudioDir: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppIdentity.ProductName),
            RoamingAuswertungProDir: Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppIdentity.LegacyRoamingDataFolder),
            DesktopDir: Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            AppVersion: AppIdentity.Version,
            EnvironmentVariables: BuildEnvironmentSnapshot());

    private static IReadOnlyDictionary<string, string> BuildEnvironmentSnapshot()
    {
        var result = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = entry.Key?.ToString();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!key.StartsWith("SEWERSTUDIO_", StringComparison.OrdinalIgnoreCase)
                && !key.StartsWith("SEWER_", StringComparison.OrdinalIgnoreCase))
                continue;

            result[key] = entry.Value?.ToString() ?? string.Empty;
        }

        return result;
    }
}
