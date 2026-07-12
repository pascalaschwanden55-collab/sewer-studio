using System;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

public static class KnowledgeBasePaths
{
    public const string EnvironmentVariableName = "SEWERSTUDIO_KNOWLEDGE_ROOT";

    private static readonly object Sync = new();
    private static RootResolution? _cachedResolution;
    private static string? _configuredSettingsRoot;
    private static bool _migrationDone;

    public enum RootSource
    {
        EnvironmentOverride,
        PersistedSettings,
        DefaultFallback
    }

    public sealed record RootResolution(
        string Root,
        RootSource Source,
        string? EnvironmentRoot,
        string? PersistedSettingsRoot)
    {
        public bool HasEnvironmentSettingsMismatch =>
            Source == RootSource.EnvironmentOverride
            && !string.IsNullOrWhiteSpace(PersistedSettingsRoot)
            && !PathsEqual(Root, PersistedSettingsRoot);
    }

    public static string GetRoot(string? settingsOverride = null)
    {
        lock (Sync)
        {
            if (_cachedResolution is not null && settingsOverride is null)
                return _cachedResolution.Root;

            var resolution = Resolve(settingsOverride);
            Directory.CreateDirectory(resolution.Root);

            if (resolution.Source == RootSource.DefaultFallback && !_migrationDone)
            {
                _migrationDone = true;
                TryMigrateFromAppData(resolution.Root);
            }

            if (settingsOverride is null)
                _cachedResolution = resolution;

            return resolution.Root;
        }
    }

    /// <summary>
    /// Liefert Pfad und Herkunft nach derselben Reihenfolge wie GetRoot:
    /// Umgebungsvariable, gespeicherte Einstellung, bisheriger AppData-Fallback.
    /// </summary>
    public static RootResolution GetResolution()
    {
        lock (Sync)
            return _cachedResolution ?? Resolve(settingsOverride: null);
    }

    public static string GetKnowledgeDbPath(string? settingsOverride = null)
        => Path.Combine(GetRoot(settingsOverride), "KnowledgeBase.db");

    public static string GetTrainingSamplesPath(string? settingsOverride = null)
        => Path.Combine(GetRoot(settingsOverride), "training_samples.json");

    public static string GetTrainingSettingsPath(string? settingsOverride = null)
        => Path.Combine(GetRoot(settingsOverride), "training_settings.json");

    public static string GetFramesDir(string? settingsOverride = null)
    {
        var dir = Path.Combine(GetRoot(settingsOverride), "frames");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetMeasuresLearningPath(string? settingsOverride = null)
        => Path.Combine(GetRoot(settingsOverride), "measures_learning.json");

    public static string GetMeasuresModelPath(string? settingsOverride = null)
        => Path.Combine(GetRoot(settingsOverride), "measures-model.zip");

    public static void InvalidateCache()
    {
        lock (Sync)
            _cachedResolution = null;
    }

    /// <summary>
    /// Setzt den dauerhaft gespeicherten KB-Pfad aus den App-Einstellungen.
    /// Eine gesetzte SEWERSTUDIO_KNOWLEDGE_ROOT-Variable bleibt der hoechste Override.
    /// </summary>
    public static void ConfigureSettingsRoot(string? settingsRoot)
    {
        lock (Sync)
        {
            _configuredSettingsRoot = Clean(settingsRoot);
            _cachedResolution = null;
        }
    }

    public static string LegacyKnowledgeDbPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuswertungPro", "KiVideoanalyse", "KnowledgeBase.db");

    public static string LegacyTrainingSamplesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuswertungPro", "training_center_samples.json");

    public static string LegacyTrainingSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuswertungPro", "training_center_settings.json");

    public static string LegacyFramesDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuswertungPro", "frames");

    public static string LegacyMeasuresLearningPath => Path.Combine(
        GetAppDataDir(), "data", "measures_learning.json");

    public static string LegacyMeasuresModelPath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Data", "measures-model.zip");

    private static RootResolution Resolve(string? settingsOverride)
    {
        var envRoot = Clean(Environment.GetEnvironmentVariable(EnvironmentVariableName));
        var configured = Clean(settingsOverride) ?? _configuredSettingsRoot;

        if (!string.IsNullOrWhiteSpace(envRoot))
            return new RootResolution(envRoot, RootSource.EnvironmentOverride, envRoot, configured);

        if (!string.IsNullOrWhiteSpace(configured))
            return new RootResolution(configured, RootSource.PersistedSettings, null, configured);

        return new RootResolution(
            Path.Combine(GetAppDataDir(), "Knowledge"),
            RootSource.DefaultFallback,
            null,
            null);
    }

    private static string? Clean(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : path.Trim();

    private static bool PathsEqual(string left, string right)
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

    private static void TryMigrateFromAppData(string knowledgeRoot)
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

            if (Directory.Exists(LegacyFramesDir))
            {
                var newFramesDir = Path.Combine(knowledgeRoot, "frames");
                Directory.CreateDirectory(newFramesDir);
                foreach (var png in Directory.EnumerateFiles(LegacyFramesDir, "*.png"))
                {
                    var dest = Path.Combine(newFramesDir, Path.GetFileName(png));
                    if (!File.Exists(dest))
                        File.Copy(png, dest);
                }
            }
        }
        catch
        {
            // Migration darf den App-Start nicht blockieren.
        }
    }

    private static void TryCopyFile(string source, string destination)
    {
        try
        {
            if (!File.Exists(source) || File.Exists(destination))
                return;

            var dir = Path.GetDirectoryName(destination);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            File.Copy(source, destination);
        }
        catch
        {
            // Best effort.
        }
    }

    private static string GetAppDataDir()
        => AppDataPathResolver.Resolve();
}
