using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Export/Import aller KI-Lerndaten und Einstellungen als ZIP-Archiv.
/// Exportiert aus dem portablen Knowledge-Ordner.
/// </summary>
public static class KnowledgeBackupService
{
    public sealed record BackupResult(bool Success, string? Error, int FileCount, long SizeBytes);

    // ── Export ────────────────────────────────────────────────────────

    public static async Task<BackupResult> ExportAsync(
        string zipPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            AppSettings.FlushPendingSave();

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            int fileCount = 0;
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var (source, entry) in EnumerateBackupFiles())
                {
                    ct.ThrowIfCancellationRequested();
                    if (!File.Exists(source))
                        continue;

                    progress?.Report($"Exportiere: {Path.GetFileName(source)}");

                    var zipEntry = zip.CreateEntry(entry, CompressionLevel.Fastest);
                    using var dest = zipEntry.Open();
                    // FileShare.ReadWrite: SQLite DB may be open
                    using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    await src.CopyToAsync(dest, ct).ConfigureAwait(false);
                    fileCount++;
                }

                // Manifest
                var manifest = new
                {
                    Version = 1,
                    Product = "SewerStudio",
                    ExportedUtc = DateTime.UtcNow.ToString("o"),
                    FileCount = fileCount
                };
                var manifestEntry = zip.CreateEntry("_manifest.json", CompressionLevel.Fastest);
                using var mStream = manifestEntry.Open();
                await JsonSerializer.SerializeAsync(mStream, manifest, cancellationToken: ct)
                    .ConfigureAwait(false);
            }

            var size = new FileInfo(zipPath).Length;
            progress?.Report($"Export abgeschlossen: {fileCount} Dateien, {size / (1024.0 * 1024.0):F1} MB");
            return new BackupResult(true, null, fileCount, size);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new BackupResult(false, ex.Message, 0, 0);
        }
    }

    // ── Import ────────────────────────────────────────────────────────

    public static async Task<BackupResult> ImportAsync(
        string zipPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            AppSettings.FlushPendingSave();

            int fileCount = 0;
            long totalBytes = 0;

            using var zip = ZipFile.OpenRead(zipPath);

            foreach (var entry in zip.Entries)
            {
                ct.ThrowIfCancellationRequested();

                if (entry.FullName == "_manifest.json" || string.IsNullOrEmpty(entry.Name))
                    continue;

                var targetPath = MapEntryToLocalPath(entry.FullName);
                if (targetPath is null)
                    continue;

                progress?.Report($"Importiere: {entry.Name}");

                var dir = Path.GetDirectoryName(targetPath);
                if (dir is not null)
                    Directory.CreateDirectory(dir);

                using var src = entry.Open();
                using var dest = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await src.CopyToAsync(dest, ct).ConfigureAwait(false);

                fileCount++;
                totalBytes += entry.Length;
            }

            progress?.Report($"Import abgeschlossen: {fileCount} Dateien");
            return new BackupResult(true, null, fileCount, totalBytes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new BackupResult(false, ex.Message, 0, 0);
        }
    }

    // ── Path helpers ─────────────────────────────────────────────────

    private static readonly string RoamingSs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppIdentity.ProductName);

    private static readonly string LocalSs = AppSettings.AppDataDir;

    private static IEnumerable<(string Source, string Entry)> EnumerateBackupFiles()
    {
        var kr = KnowledgeRoot.GetRoot();

        // KnowledgeBase DB + WAL/SHM (aus Knowledge-Ordner)
        var dbPath = KnowledgeRoot.GetKnowledgeDbPath();
        yield return (dbPath, "knowledge/KnowledgeBase.db");
        yield return (dbPath + "-wal", "knowledge/KnowledgeBase.db-wal");
        yield return (dbPath + "-shm", "knowledge/KnowledgeBase.db-shm");

        // Training data (aus Knowledge-Ordner)
        yield return (KnowledgeRoot.GetTrainingSamplesPath(), "knowledge/training_samples.json");
        yield return (KnowledgeRoot.GetTrainingSettingsPath(), "knowledge/training_settings.json");
        yield return (KnowledgeRoot.GetMeasuresLearningPath(), "knowledge/measures_learning.json");
        yield return (KnowledgeRoot.GetMeasuresModelPath(), "knowledge/measures-model.zip");

        // Frames (aus Knowledge-Ordner)
        var framesDir = KnowledgeRoot.GetFramesDir();
        if (Directory.Exists(framesDir))
        {
            foreach (var png in Directory.EnumerateFiles(framesDir, "*.png"))
                yield return (png, "knowledge/frames/" + Path.GetFileName(png));
        }

        // SewerStudio dropdowns + presets (bleiben in AppData)
        var dropdownsDir = Path.Combine(RoamingSs, "dropdowns");
        if (Directory.Exists(dropdownsDir))
        {
            foreach (var json in Directory.EnumerateFiles(dropdownsDir, "*.json"))
                yield return (json, "roaming_sewerstudio/dropdowns/" + Path.GetFileName(json));
        }
        yield return (Path.Combine(RoamingSs, "presets.json"), "roaming_sewerstudio/presets.json");

        // Local settings
        yield return (Path.Combine(LocalSs, "settings.json"), "local_sewerstudio/settings.json");
    }

    private static string? MapEntryToLocalPath(string entryName)
    {
        const string prefixKnowledge = "knowledge/";
        const string prefixSs = "roaming_sewerstudio/";
        const string prefixLocal = "local_sewerstudio/";
        // Legacy-Prefix fuer aeltere Backups
        const string prefixAp = "roaming_auswertungpro/";

        if (entryName.StartsWith(prefixKnowledge))
            return Path.Combine(KnowledgeRoot.GetRoot(), entryName[prefixKnowledge.Length..].Replace('/', Path.DirectorySeparatorChar));
        if (entryName.StartsWith(prefixAp))
        {
            // Alte Backups → in Knowledge-Ordner importieren
            var relative = entryName[prefixAp.Length..].Replace('/', Path.DirectorySeparatorChar);
            // KiVideoanalyse/KnowledgeBase.db → KnowledgeBase.db
            if (relative.StartsWith("KiVideoanalyse" + Path.DirectorySeparatorChar))
                relative = relative[("KiVideoanalyse" + Path.DirectorySeparatorChar).Length..];
            return Path.Combine(KnowledgeRoot.GetRoot(), relative);
        }
        if (entryName.StartsWith(prefixSs))
            return Path.Combine(RoamingSs, entryName[prefixSs.Length..].Replace('/', Path.DirectorySeparatorChar));
        if (entryName.StartsWith(prefixLocal))
            return Path.Combine(LocalSs, entryName[prefixLocal.Length..].Replace('/', Path.DirectorySeparatorChar));

        return null;
    }
}
