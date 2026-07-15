using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

public interface IKnowledgeBasePathService
{
    string GetRoot(string? settingsOverride = null);

    KnowledgeBasePaths.RootResolution GetResolution();

    string GetKnowledgeDbPath(string? settingsOverride = null);

    string GetTrainingSamplesPath(string? settingsOverride = null);

    string GetTrainingSettingsPath(string? settingsOverride = null);

    string GetFramesDir(string? settingsOverride = null);

    string GetMeasuresLearningPath(string? settingsOverride = null);

    string GetMeasuresModelPath(string? settingsOverride = null);

    string LegacyKnowledgeDbPath { get; }

    string LegacyTrainingSamplesPath { get; }

    string LegacyTrainingSettingsPath { get; }

    string LegacyFramesDir { get; }

    string LegacyMeasuresLearningPath { get; }

    string LegacyMeasuresModelPath { get; }

    void InvalidateCache();

    void ConfigureSettingsRoot(string? settingsRoot);
}

/// <summary>
/// Loest alle Wissenspfade einmal pro Instanz auf und uebernimmt alte Wissensdateien
/// nur beim lokalen Standardpfad. Explizite Projekt- oder Testpfade bleiben unangetastet.
/// </summary>
public sealed class KnowledgeBasePathService : IKnowledgeBasePathService
{
    private readonly object _sync = new();
    private readonly Func<string?> _getEnvironmentRoot;
    private readonly Func<string> _getRoamingAppData;
    private readonly Func<string> _getAppDataDir;
    private readonly Func<string> _getBaseDirectory;
    private KnowledgeBasePaths.RootResolution? _cachedResolution;
    private string? _configuredSettingsRoot;
    private bool _migrationDone;

    public KnowledgeBasePathService()
        : this(
            () => Environment.GetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName),
            () => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            () => AppDataPathResolver.Resolve(),
            () => AppDomain.CurrentDomain.BaseDirectory)
    {
    }

    internal KnowledgeBasePathService(
        Func<string?> getEnvironmentRoot,
        Func<string> getRoamingAppData,
        Func<string> getAppDataDir,
        Func<string> getBaseDirectory)
    {
        _getEnvironmentRoot = getEnvironmentRoot ?? throw new ArgumentNullException(nameof(getEnvironmentRoot));
        _getRoamingAppData = getRoamingAppData ?? throw new ArgumentNullException(nameof(getRoamingAppData));
        _getAppDataDir = getAppDataDir ?? throw new ArgumentNullException(nameof(getAppDataDir));
        _getBaseDirectory = getBaseDirectory ?? throw new ArgumentNullException(nameof(getBaseDirectory));
    }

    public string GetRoot(string? settingsOverride = null)
    {
        lock (_sync)
        {
            if (_cachedResolution is not null && settingsOverride is null)
                return _cachedResolution.Root;

            var resolution = Resolve(settingsOverride);
            Directory.CreateDirectory(resolution.Root);

            if (resolution.Source == KnowledgeBasePaths.RootSource.DefaultFallback && !_migrationDone)
            {
                _migrationDone = true;
                TryMigrateFromAppData(resolution.Root);
            }

            if (settingsOverride is null)
                _cachedResolution = resolution;

            return resolution.Root;
        }
    }

    public KnowledgeBasePaths.RootResolution GetResolution()
    {
        lock (_sync)
            return _cachedResolution ?? Resolve(settingsOverride: null);
    }

    public string GetKnowledgeDbPath(string? settingsOverride = null) =>
        Path.Combine(GetRoot(settingsOverride), "KnowledgeBase.db");

    public string GetTrainingSamplesPath(string? settingsOverride = null) =>
        Path.Combine(GetRoot(settingsOverride), "training_samples.json");

    public string GetTrainingSettingsPath(string? settingsOverride = null) =>
        Path.Combine(GetRoot(settingsOverride), "training_settings.json");

    public string GetFramesDir(string? settingsOverride = null)
    {
        var dir = Path.Combine(GetRoot(settingsOverride), "frames");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetMeasuresLearningPath(string? settingsOverride = null) =>
        Path.Combine(GetRoot(settingsOverride), "measures_learning.json");

    public string GetMeasuresModelPath(string? settingsOverride = null) =>
        Path.Combine(GetRoot(settingsOverride), "measures-model.zip");

    public string LegacyKnowledgeDbPath => Path.Combine(
        _getRoamingAppData(),
        "AuswertungPro",
        "KiVideoanalyse",
        "KnowledgeBase.db");

    public string LegacyTrainingSamplesPath => Path.Combine(
        _getRoamingAppData(),
        "AuswertungPro",
        "training_center_samples.json");

    public string LegacyTrainingSettingsPath => Path.Combine(
        _getRoamingAppData(),
        "AuswertungPro",
        "training_center_settings.json");

    public string LegacyFramesDir => Path.Combine(
        _getRoamingAppData(),
        "AuswertungPro",
        "frames");

    public string LegacyMeasuresLearningPath => Path.Combine(
        _getAppDataDir(),
        "data",
        "measures_learning.json");

    public string LegacyMeasuresModelPath => Path.Combine(
        _getBaseDirectory(),
        "Data",
        "measures-model.zip");

    public void InvalidateCache()
    {
        lock (_sync)
            _cachedResolution = null;
    }

    public void ConfigureSettingsRoot(string? settingsRoot)
    {
        lock (_sync)
        {
            _configuredSettingsRoot = CleanAbsoluteRoot(
                settingsRoot,
                "Der gespeicherte Wissensdatenbank-Pfad");
            _cachedResolution = null;
        }
    }

    private KnowledgeBasePaths.RootResolution Resolve(string? settingsOverride)
    {
        var envRoot = CleanAbsoluteRoot(
            _getEnvironmentRoot(),
            $"Die Umgebungsvariable {KnowledgeBasePaths.EnvironmentVariableName}");
        var configured = CleanAbsoluteRoot(
            settingsOverride,
            "Der angegebene Wissensdatenbank-Pfad") ?? _configuredSettingsRoot;

        if (!string.IsNullOrWhiteSpace(envRoot))
        {
            return new KnowledgeBasePaths.RootResolution(
                envRoot,
                KnowledgeBasePaths.RootSource.EnvironmentOverride,
                envRoot,
                configured);
        }

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return new KnowledgeBasePaths.RootResolution(
                configured,
                KnowledgeBasePaths.RootSource.PersistedSettings,
                null,
                configured);
        }

        return new KnowledgeBasePaths.RootResolution(
            Path.Combine(_getAppDataDir(), "Knowledge"),
            KnowledgeBasePaths.RootSource.DefaultFallback,
            null,
            null);
    }

    private static string? CleanAbsoluteRoot(string? path, string sourceDescription)
    {
        var cleaned = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        if (cleaned is null)
            return null;

        try
        {
            if (Path.IsPathFullyQualified(cleaned))
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(cleaned));
        }
        catch (Exception ex) when (ex is ArgumentException
                                   or NotSupportedException
                                   or PathTooLongException)
        {
            // Die gemeinsame Warnung unten reicht aus und enthaelt keinen Rohwert.
        }

        BestEffort.ReportWarning(
            $"[KnowledgeBase] {sourceDescription} wurde ignoriert, weil er kein gueltiger absoluter Pfad ist.");
        return null;
    }

    internal static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private void TryMigrateFromAppData(string knowledgeRoot)
    {
        try
        {
            var newDbPath = Path.Combine(knowledgeRoot, "KnowledgeBase.db");
            if (File.Exists(newDbPath))
                return;

            TryCopyFile(LegacyKnowledgeDbPath, newDbPath);
            TryCopyFile(LegacyKnowledgeDbPath + "-wal", newDbPath + "-wal");
            TryCopyFile(LegacyKnowledgeDbPath + "-shm", newDbPath + "-shm");
            TryCopyFile(LegacyTrainingSamplesPath, Path.Combine(knowledgeRoot, "training_samples.json"));
            TryCopyFile(LegacyTrainingSettingsPath, Path.Combine(knowledgeRoot, "training_settings.json"));
            TryCopyFile(LegacyMeasuresLearningPath, Path.Combine(knowledgeRoot, "measures_learning.json"));
            TryCopyFile(LegacyMeasuresModelPath, Path.Combine(knowledgeRoot, "measures-model.zip"));

            if (!Directory.Exists(LegacyFramesDir))
                return;

            var newFramesDir = Path.Combine(knowledgeRoot, "frames");
            Directory.CreateDirectory(newFramesDir);
            foreach (var png in Directory.EnumerateFiles(LegacyFramesDir, "*.png"))
            {
                var destination = Path.Combine(newFramesDir, Path.GetFileName(png));
                if (!File.Exists(destination))
                    File.Copy(png, destination);
            }
        }
        catch
        {
            // Migration darf den Programmstart nicht blockieren.
        }
    }

    private static void TryCopyFile(string source, string destination)
    {
        try
        {
            if (!File.Exists(source) || File.Exists(destination))
                return;

            var directory = Path.GetDirectoryName(destination);
            if (directory is not null)
                Directory.CreateDirectory(directory);

            File.Copy(source, destination);
        }
        catch
        {
            // Best effort: eine alte Einzeldatei darf den Programmstart nicht blockieren.
        }
    }
}
