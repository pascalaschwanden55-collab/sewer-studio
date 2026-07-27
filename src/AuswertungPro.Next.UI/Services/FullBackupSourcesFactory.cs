using System.Collections;
using System.IO;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.UI.Services;

public interface IFullBackupSourcesProvider
{
    FullBackupSources Resolve(AppSettings? settings = null);
}

/// <summary>Ermittelt die zum Sicherungszeitpunkt gueltigen Quellpfade.</summary>
public sealed class FullBackupSourcesProvider : IFullBackupSourcesProvider
{
    private readonly IRepositoryRootLocator _repositoryRootLocator;
    private readonly Func<string> _getKnowledgeRoot;
    private readonly string _localSewerStudioDir;
    private readonly Func<Environment.SpecialFolder, string> _getFolderPath;
    private readonly Func<IDictionary> _getEnvironmentVariables;
    private readonly string _baseDirectory;
    private readonly string _appVersion;

    public FullBackupSourcesProvider(
        IRepositoryRootLocator repositoryRootLocator,
        Func<string>? getKnowledgeRoot = null,
        string? localSewerStudioDir = null,
        Func<Environment.SpecialFolder, string>? getFolderPath = null,
        Func<IDictionary>? getEnvironmentVariables = null,
        string? baseDirectory = null,
        string? appVersion = null)
    {
        _repositoryRootLocator = repositoryRootLocator
            ?? throw new ArgumentNullException(nameof(repositoryRootLocator));
        _getKnowledgeRoot = getKnowledgeRoot ?? (() => KnowledgeBasePaths.GetRoot());
        _localSewerStudioDir = localSewerStudioDir ?? AppSettings.AppDataDir;
        _getFolderPath = getFolderPath ?? Environment.GetFolderPath;
        _getEnvironmentVariables = getEnvironmentVariables ?? Environment.GetEnvironmentVariables;
        _baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
        _appVersion = appVersion ?? AppIdentity.Version;
    }

    public FullBackupSources Resolve(AppSettings? settings = null)
    {
        var projectRoots = BuildProjectRoots(settings);
        return new FullBackupSources(
            RepoRoot: _repositoryRootLocator.Locate(_baseDirectory),
            KnowledgeRoot: _getKnowledgeRoot(),
            LocalSewerStudioDir: _localSewerStudioDir,
            RoamingSewerStudioDir: Path.Combine(
                _getFolderPath(Environment.SpecialFolder.ApplicationData),
                AppIdentity.ProductName),
            RoamingAuswertungProDir: Path.Combine(
                _getFolderPath(Environment.SpecialFolder.ApplicationData),
                AppIdentity.LegacyRoamingDataFolder),
            DesktopDir: _getFolderPath(Environment.SpecialFolder.DesktopDirectory),
            AppVersion: _appVersion,
            EnvironmentVariables: BuildEnvironmentSnapshot(),
            ProjectRoots: projectRoots.Required,
            IncludeProjectVideos: settings?.FullBackupIncludeProjectVideos ?? false,
            OptionalProjectRoots: projectRoots.Optional);
    }

    private static ProjectRootSelection BuildProjectRoots(AppSettings? settings)
    {
        if (settings is null)
            return new ProjectRootSelection([], []);

        var required = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.ProjectsRootDirectory))
            AddDistinct(required, settings.ProjectsRootDirectory);

        var lastProjectRoot = ProjectFileLocator.ProjectRootFromFile(settings.LastProjectPath);
        if (!string.IsNullOrWhiteSpace(lastProjectRoot))
            AddDistinct(required, lastProjectRoot);

        var optional = new List<string>();
        foreach (var projectPath in settings.RecentProjectPaths)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                continue;
            var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectPath);
            if (string.IsNullOrWhiteSpace(projectRoot)
                || required.Contains(projectRoot, StringComparer.OrdinalIgnoreCase))
                continue;
            AddDistinct(optional, projectRoot);
        }

        return new ProjectRootSelection(required, optional);
    }

    private static void AddDistinct(List<string> roots, string root)
    {
        if (!roots.Contains(root, StringComparer.OrdinalIgnoreCase))
            roots.Add(root);
    }

    private IReadOnlyDictionary<string, string> BuildEnvironmentSnapshot()
    {
        var result = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in _getEnvironmentVariables())
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

    private sealed record ProjectRootSelection(
        IReadOnlyList<string> Required,
        IReadOnlyList<string> Optional);
}

/// <summary>Kompatibilitaetsfassade fuer bestehende Aufrufer.</summary>
public static class FullBackupSourcesFactory
{
    private static readonly IFullBackupSourcesProvider Default =
        new FullBackupSourcesProvider(new RepositoryRootFileLocator());

    public static IFullBackupSourcesProvider Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IFullBackupSourcesProvider provider) =>
        throw new NotSupportedException(
            "Die globale Sicherungsquellen-Suche kann nicht mehr ausgetauscht werden. " +
            "IFullBackupSourcesProvider bitte per Konstruktor uebergeben.");

    public static FullBackupSources ErmittleAktuelleQuellen(AppSettings? settings = null) =>
        Current.Resolve(settings);

    internal static FullBackupSources ErmittleAktuelleQuellen(
        AppSettings? settings,
        IRepositoryRootLocator repositoryRootLocator) =>
        new FullBackupSourcesProvider(repositoryRootLocator).Resolve(settings);
}
