namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>Kompatible statische API fuer die zentral aufgeloesten Wissenspfade.</summary>
public static class KnowledgeBasePaths
{
    public const string EnvironmentVariableName = "SEWERSTUDIO_KNOWLEDGE_ROOT";

    private static IKnowledgeBasePathService _current = new KnowledgeBasePathService();

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
            && !KnowledgeBasePathService.PathsEqual(Root, PersistedSettingsRoot);
    }

    public static IKnowledgeBasePathService Current => Volatile.Read(ref _current);

    public static void Use(IKnowledgeBasePathService paths) =>
        Volatile.Write(ref _current, paths ?? throw new ArgumentNullException(nameof(paths)));

    public static string GetRoot(string? settingsOverride = null) =>
        Current.GetRoot(settingsOverride);

    public static RootResolution GetResolution() => Current.GetResolution();

    public static string GetKnowledgeDbPath(string? settingsOverride = null) =>
        Current.GetKnowledgeDbPath(settingsOverride);

    public static string GetTrainingSamplesPath(string? settingsOverride = null) =>
        Current.GetTrainingSamplesPath(settingsOverride);

    public static string GetTrainingSettingsPath(string? settingsOverride = null) =>
        Current.GetTrainingSettingsPath(settingsOverride);

    public static string GetFramesDir(string? settingsOverride = null) =>
        Current.GetFramesDir(settingsOverride);

    public static string GetMeasuresLearningPath(string? settingsOverride = null) =>
        Current.GetMeasuresLearningPath(settingsOverride);

    public static string GetMeasuresModelPath(string? settingsOverride = null) =>
        Current.GetMeasuresModelPath(settingsOverride);

    public static void InvalidateCache() => Current.InvalidateCache();

    public static void ConfigureSettingsRoot(string? settingsRoot) =>
        Current.ConfigureSettingsRoot(settingsRoot);

    public static string LegacyKnowledgeDbPath => Current.LegacyKnowledgeDbPath;

    public static string LegacyTrainingSamplesPath => Current.LegacyTrainingSamplesPath;

    public static string LegacyTrainingSettingsPath => Current.LegacyTrainingSettingsPath;

    public static string LegacyFramesDir => Current.LegacyFramesDir;

    public static string LegacyMeasuresLearningPath => Current.LegacyMeasuresLearningPath;

    public static string LegacyMeasuresModelPath => Current.LegacyMeasuresModelPath;
}
