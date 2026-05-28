using System;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

public static class KnowledgeBasePaths
{
    private static string? _cachedRoot;
    private static bool _migrationDone;

    public static string GetRoot(string? settingsOverride = null)
    {
        if (_cachedRoot is not null && settingsOverride is null)
            return _cachedRoot;

        var root = ResolveRoot(settingsOverride);
        Directory.CreateDirectory(root);

        if (ShouldRunLegacyMigration(settingsOverride) && !_migrationDone)
        {
            _migrationDone = true;
            TryMigrateFromAppData(root);
        }

        if (settingsOverride is null)
            _cachedRoot = root;

        return root;
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

    public static void InvalidateCache() => _cachedRoot = null;

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

    private static string ResolveRoot(string? settingsOverride)
    {
        if (!string.IsNullOrWhiteSpace(settingsOverride))
            return settingsOverride;

        var envRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT")?.Trim();
        if (!string.IsNullOrWhiteSpace(envRoot))
            return envRoot;

        return Path.Combine(GetAppDataDir(), "Knowledge");
    }

    private static bool ShouldRunLegacyMigration(string? settingsOverride)
    {
        if (!string.IsNullOrWhiteSpace(settingsOverride))
            return false;

        var envRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT")?.Trim();
        return string.IsNullOrWhiteSpace(envRoot);
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
    {
        var overridePath = Environment.GetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR");
        return string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SewerStudio")
            : overridePath;
    }
}
