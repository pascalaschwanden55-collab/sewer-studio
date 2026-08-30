using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Maintenance;
using AuswertungPro.Next.Infrastructure.Maintenance;

namespace AuswertungPro.Next.UI.Settings;

public static class SettingsProgramCleanupRequestFactory
{
    private static readonly IProgramRootLocator DefaultProgramRootLocator =
        new ProgramRootFileLocator();

    internal static IProgramRootLocator CompatibilityService
        => DefaultProgramRootLocator;

    public static ProgramCleanupRequest Create(AppSettings settings, DateTime utcNow)
        => Create(
            settings,
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.GetTempPath(),
            utcNow,
            CompatibilityService);

    internal static ProgramCleanupRequest Create(
        AppSettings settings,
        DateTime utcNow,
        IProgramRootLocator programRootLocator)
        => Create(
            settings,
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Path.GetTempPath(),
            utcNow,
            programRootLocator);

    internal static ProgramCleanupRequest Create(
        AppSettings settings,
        string appBaseDirectory,
        string currentDirectory,
        string systemTempRoot,
        DateTime utcNow)
        => Create(
            settings,
            appBaseDirectory,
            currentDirectory,
            systemTempRoot,
            utcNow,
            CompatibilityService);

    internal static ProgramCleanupRequest Create(
        AppSettings settings,
        string appBaseDirectory,
        string currentDirectory,
        string systemTempRoot,
        DateTime utcNow,
        IProgramRootLocator programRootLocator)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(programRootLocator);

        return new ProgramCleanupRequest(
            programRootLocator.FindProgramRoot(appBaseDirectory, currentDirectory),
            systemTempRoot,
            appBaseDirectory,
            BuildProtectedProjectRoots(settings),
            utcNow.AddDays(-1));
    }

    internal static string FindProgramRoot(string appBaseDirectory, string currentDirectory)
        => CompatibilityService.FindProgramRoot(appBaseDirectory, currentDirectory);

    private static IReadOnlyCollection<string> BuildProtectedProjectRoots(AppSettings settings)
    {
        var protectedRoots = new List<string?>
        {
            TryNormalizeDirectory(settings.ProjectsRootDirectory),
            TryNormalizeDirectory(settings.LastVideoSourceFolder),
            TryNormalizeDirectory(settings.LastVideoFolder),
            TryNormalizeDirectory(settings.LastDistributionTargetFolder),
            TryNormalizeDirectory(settings.KantonUriXtfDirectory)
        };

        var paths = new List<string?> { settings.LastProjectPath };
        paths.AddRange(settings.RecentProjectPaths);
        paths.AddRange(settings.HiddenProjectPaths);

        protectedRoots.AddRange(paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(TryResolveProjectRoot)
            .Where(path => !string.IsNullOrWhiteSpace(path)));

        return protectedRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? TryNormalizeDirectory(string? path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryResolveProjectRoot(string? projectPath)
    {
        try
        {
            var root = ProjectFileLocator.ProjectRootFromFile(projectPath)
                       ?? Path.GetDirectoryName(projectPath);
            return string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root);
        }
        catch
        {
            // Ein alter, ungueltiger Merkliste-Pfad darf die Bereinigung nicht blockieren.
            return null;
        }
    }
}
