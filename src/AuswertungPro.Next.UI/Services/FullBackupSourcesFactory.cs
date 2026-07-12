using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.UI.Services;

public static class FullBackupSourcesFactory
{
    public static FullBackupSources ErmittleAktuelleQuellen(AppSettings? settings = null)
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
            EnvironmentVariables: BuildEnvironmentSnapshot(),
            ProjectRoots: BuildProjectRoots(settings),
            IncludeProjectVideos: settings?.FullBackupIncludeProjectVideos ?? false);

    private static IReadOnlyList<string> BuildProjectRoots(AppSettings? settings)
    {
        if (settings is null)
            return Array.Empty<string>();

        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.ProjectsRootDirectory))
            roots.Add(settings.ProjectsRootDirectory);

        foreach (var projectPath in settings.RecentProjectPaths.Prepend(settings.LastProjectPath))
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                continue;
            var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectPath);
            if (!string.IsNullOrWhiteSpace(projectRoot))
                roots.Add(projectRoot);
        }

        return roots;
    }

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
