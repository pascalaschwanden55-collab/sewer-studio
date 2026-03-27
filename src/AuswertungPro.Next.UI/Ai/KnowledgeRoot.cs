using System;
using System.IO;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Zentraler Pfad-Resolver fuer alle KI-Wissensdaten.
/// Alle Trainingsdaten, KB, Frames etc. liegen im selben Ordner,
/// damit der gesamte Programmordner portabel kopiert werden kann.
///
/// Prioritaet:
///   1. AppSettings.KnowledgeRootPath  (explizit gesetzt)
///   2. SEWERSTUDIO_KNOWLEDGE_ROOT     (Umgebungsvariable)
///   3. {AppBaseDir}\Knowledge\         (Default = portabel)
/// </summary>
public static class KnowledgeRoot
{
    private static string? _cachedRoot;
    private static bool _migrationDone;

    /// <summary>
    /// Gibt den aufgeloesten Knowledge-Root-Pfad zurueck.
    /// Erstellt den Ordner automatisch und migriert alte Daten falls noetig.
    /// </summary>
    public static string GetRoot(string? settingsOverride = null)
    {
        if (_cachedRoot is not null && settingsOverride is null)
            return _cachedRoot;

        var root = ResolveRoot(settingsOverride);
        Directory.CreateDirectory(root);

        // Einmalige Migration von AppData → Knowledge-Ordner
        if (!_migrationDone)
        {
            _migrationDone = true;
            TryMigrateFromAppData(root);
        }

        if (settingsOverride is null)
            _cachedRoot = root;

        return root;
    }

    /// <summary>Pfad zur KnowledgeBase SQLite-Datenbank.</summary>
    public static string GetKnowledgeDbPath(string? settingsOverride = null)
        => Path.Combine(GetRoot(settingsOverride), "KnowledgeBase.db");

    /// <summary>Pfad zur Training-Samples JSON-Datei.</summary>
    public static string GetTrainingSamplesPath(string? settingsOverride = null)
        => Path.Combine(GetRoot(settingsOverride), "training_samples.json");

    /// <summary>Pfad zur Training-Center Settings JSON-Datei.</summary>
    public static string GetTrainingSettingsPath(string? settingsOverride = null)
        => Path.Combine(GetRoot(settingsOverride), "training_settings.json");

    /// <summary>Pfad zum Frames-Ordner fuer extrahierte Video-Frames.</summary>
    public static string GetFramesDir(string? settingsOverride = null)
    {
        var dir = Path.Combine(GetRoot(settingsOverride), "frames");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Pfad zur Massnahmen-Lerndaten JSON-Datei.</summary>
    public static string GetMeasuresLearningPath(string? settingsOverride = null)
        => Path.Combine(GetRoot(settingsOverride), "measures_learning.json");

    /// <summary>Pfad zum trainierten Massnahmen-Modell ZIP.</summary>
    public static string GetMeasuresModelPath(string? settingsOverride = null)
        => Path.Combine(GetRoot(settingsOverride), "measures-model.zip");

    /// <summary>Cache zuruecksetzen (z.B. nach Settings-Aenderung).</summary>
    public static void InvalidateCache() => _cachedRoot = null;

    // ── Legacy-Pfade (fuer Migration) ────────────────────────────────

    /// <summary>Alter KB-Pfad in AppData (vor Portierung).</summary>
    public static string LegacyKnowledgeDbPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuswertungPro", "KiVideoanalyse", "KnowledgeBase.db");

    /// <summary>Alter Training-Samples Pfad.</summary>
    public static string LegacyTrainingSamplesPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuswertungPro", "training_center_samples.json");

    /// <summary>Alter Training-Settings Pfad.</summary>
    public static string LegacyTrainingSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuswertungPro", "training_center_settings.json");

    /// <summary>Alter Frames-Ordner.</summary>
    public static string LegacyFramesDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuswertungPro", "frames");

    /// <summary>Alter Massnahmen-Lernpfad.</summary>
    public static string LegacyMeasuresLearningPath => Path.Combine(
        AppSettings.AppDataDir, "data", "measures_learning.json");

    /// <summary>Alter Massnahmen-Modell Pfad.</summary>
    public static string LegacyMeasuresModelPath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Data", "measures-model.zip");

    // ── Interne Aufloesung ───────────────────────────────────────────

    private static string ResolveRoot(string? settingsOverride)
    {
        // 1. Expliziter Override
        if (!string.IsNullOrWhiteSpace(settingsOverride))
            return settingsOverride;

        // 2. Umgebungsvariable
        var envRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT")?.Trim();
        if (!string.IsNullOrEmpty(envRoot))
            return envRoot;

        // 3. Default: Programmordner\Knowledge (portabel)
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Knowledge");
    }

    // ── Migration von AppData → Knowledge ────────────────────────────

    /// <summary>
    /// Kopiert vorhandene KI-Daten aus den alten AppData-Pfaden in den
    /// neuen Knowledge-Ordner, sofern dieser noch keine Daten enthaelt.
    /// Wird nur einmal beim ersten Start ausgefuehrt.
    /// </summary>
    private static void TryMigrateFromAppData(string knowledgeRoot)
    {
        try
        {
            // Nur migrieren wenn der Knowledge-Ordner noch keine DB hat
            var newDbPath = Path.Combine(knowledgeRoot, "KnowledgeBase.db");
            if (File.Exists(newDbPath))
                return;

            // Einzelne Dateien kopieren
            TryCopyFile(LegacyKnowledgeDbPath, newDbPath);
            TryCopyFile(LegacyKnowledgeDbPath + "-wal", newDbPath + "-wal");
            TryCopyFile(LegacyKnowledgeDbPath + "-shm", newDbPath + "-shm");
            TryCopyFile(LegacyTrainingSamplesPath, Path.Combine(knowledgeRoot, "training_samples.json"));
            TryCopyFile(LegacyTrainingSettingsPath, Path.Combine(knowledgeRoot, "training_settings.json"));
            TryCopyFile(LegacyMeasuresLearningPath, Path.Combine(knowledgeRoot, "measures_learning.json"));
            TryCopyFile(LegacyMeasuresModelPath, Path.Combine(knowledgeRoot, "measures-model.zip"));

            // Frames-Ordner kopieren
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
            // Migration ist best-effort, darf nie den Start blockieren
        }
    }

    private static void TryCopyFile(string source, string destination)
    {
        try
        {
            if (File.Exists(source) && !File.Exists(destination))
            {
                var dir = Path.GetDirectoryName(destination);
                if (dir is not null)
                    Directory.CreateDirectory(dir);
                File.Copy(source, destination);
            }
        }
        catch { /* best-effort */ }
    }
}
