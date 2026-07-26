using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Backup;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
using InfraBackup = AuswertungPro.Next.Infrastructure.Ai.Backup;
using BackupResult = AuswertungPro.Next.UI.Services.KnowledgeBackupService.BackupResult;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Fuehrt den ZIP-Transfer und dessen Rueckrollschutz aus.
/// Dateikatalog und rechnerabhaengige Nachbearbeitung sind getrennte Bausteine.
/// </summary>
internal static class KnowledgeBackupEngine
{
    private const int ManifestVersion = BackupManifestVersionPolicy.CurrentVersion;

    /// <summary>
    /// ZIP-Eintragsname der laufenden Wissensdatenbank. Sie wird als gepruefter
    /// SQLite-Online-Snapshot gesichert; ihre WAL-/SHM-Begleiter wandern nicht mit.
    /// </summary>
    internal const string KnowledgeDatabaseEntryName = "knowledge/KnowledgeBase.db";

    internal static async Task<BackupResult> ExportAsync(
        string zipPath,
        KnowledgeBackupLocations locations,
        Action flushPendingSettings,
        Action<IProgress<string>?> flushSqliteWal,
        ISqliteSnapshotCopier sqliteSnapshots,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        string? temporaryArchivePath = null;
        try
        {
            flushPendingSettings();
            flushSqliteWal(progress);

            var destinationPath = Path.GetFullPath(zipPath);
            var destinationDirectory = Path.GetDirectoryName(destinationPath)
                ?? throw new InvalidOperationException($"Zielordner fehlt: {zipPath}");
            temporaryArchivePath = Path.Combine(
                destinationDirectory,
                $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

            var fileCount = 0;
            using (var zip = ZipFile.Open(temporaryArchivePath, ZipArchiveMode.Create))
            {
                foreach (var (source, entryName) in KnowledgeBackupFileCatalog.EnumerateBackupFiles(locations))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!File.Exists(source))
                        continue;

                    if (IsKnowledgeDatabaseCompanionEntry(entryName))
                    {
                        // Der Snapshot ist eigenstaendig; WAL-/SHM-Reste der
                        // laufenden Datenbank gehoeren nicht ins Archiv.
                        continue;
                    }

                    progress?.Report($"Exportiere: {Path.GetFileName(source)}");
                    if (string.Equals(entryName, KnowledgeDatabaseEntryName, StringComparison.Ordinal))
                    {
                        await WriteVerifiedSqliteSnapshotAsync(
                                zip,
                                sqliteSnapshots,
                                source,
                                entryName,
                                destinationDirectory,
                                ct)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                        await using var destination = entry.Open();
                        await using var sourceStream = new FileStream(
                            source,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite);
                        await sourceStream.CopyToAsync(destination, ct).ConfigureAwait(false);
                    }

                    fileCount++;
                }

                var manifest = new
                {
                    Version = ManifestVersion,
                    Product = "SewerStudio",
                    ExportedUtc = DateTime.UtcNow.ToString("o"),
                    FileCount = fileCount,
                    locations.KnowledgeRoot
                };
                var manifestEntry = zip.CreateEntry("_manifest.json", CompressionLevel.Fastest);
                await using var manifestStream = manifestEntry.Open();
                await JsonSerializer.SerializeAsync(
                        manifestStream,
                        manifest,
                        cancellationToken: ct)
                    .ConfigureAwait(false);
            }

            CommitArchive(temporaryArchivePath, destinationPath);
            temporaryArchivePath = null;

            var size = new FileInfo(destinationPath).Length;
            progress?.Report(
                $"Export abgeschlossen: {fileCount} Dateien, {size / (1024.0 * 1024.0):F1} MB");
            return new BackupResult(true, null, fileCount, size);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[KnowledgeBackup] Export fehlgeschlagen ({zipPath}): {ex.GetType().Name}: {ex.Message}");
            return new BackupResult(false, UserError.Describe(ex), 0, 0);
        }
        finally
        {
            if (temporaryArchivePath is not null)
            {
                BestEffort.Try(
                    () =>
                    {
                        if (File.Exists(temporaryArchivePath))
                            File.Delete(temporaryArchivePath);
                    },
                    $"Knowledge-Export: Temp-Datei {temporaryArchivePath} loeschen");
            }
        }
    }

    private static bool IsKnowledgeDatabaseCompanionEntry(string entryName)
        => string.Equals(entryName, KnowledgeDatabaseEntryName + "-wal", StringComparison.Ordinal)
           || string.Equals(entryName, KnowledgeDatabaseEntryName + "-shm", StringComparison.Ordinal);

    /// <summary>
    /// Schreibt die laufende Wissensdatenbank als geprueften Online-Snapshot
    /// (SQLite-Backup-API, derselbe Mechanismus wie im Echtzeit-Spiegel) statt
    /// als Rohkopie ins Archiv. Der gepruefte Stand besteht die Inhaltspruefung
    /// bereits im SqliteSnapshotCopyService.
    /// </summary>
    private static async Task WriteVerifiedSqliteSnapshotAsync(
        ZipArchive zip,
        ISqliteSnapshotCopier sqliteSnapshots,
        string databasePath,
        string entryName,
        string destinationDirectory,
        CancellationToken ct)
    {
        var snapshotPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(entryName)}.{Guid.NewGuid():N}.snapshot");
        try
        {
            await sqliteSnapshots
                .CreateVerifiedSnapshotAsync(databasePath, snapshotPath, null, ct)
                .ConfigureAwait(false);

            var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
            await using var destination = entry.Open();
            await using var snapshotStream = new FileStream(
                snapshotPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            await snapshotStream.CopyToAsync(destination, ct).ConfigureAwait(false);
        }
        finally
        {
            BestEffort.Try(
                () =>
                {
                    if (File.Exists(snapshotPath))
                        File.Delete(snapshotPath);
                },
                $"Knowledge-Export: SQLite-Snapshot {snapshotPath} loeschen");
        }
    }

    internal static async Task<BackupResult> ImportAsync(
        string zipPath,
        KnowledgeBackupLocations locations,
        Action flushPendingSettings,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            flushPendingSettings();
            using var zip = ZipFile.OpenRead(zipPath);

            var incompatibleResult = await CheckManifestAsync(zip, ct).ConfigureAwait(false);
            if (incompatibleResult is not null)
                return incompatibleResult;

            var filesToImport = CollectImportFiles(zip, locations);
            if (filesToImport.Count == 0)
                return new BackupResult(
                    false,
                    "Keine importierbaren Dateien im Archiv gefunden.",
                    0,
                    0);

            var backupDirectory = Path.Combine(
                locations.TempRoot,
                $"sewerstudio_import_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(backupDirectory);
            var backedUpFiles = new List<(string Original, string Backup)>();
            var newlyCreatedFiles = new List<string>();

            try
            {
                BackupExistingFiles(filesToImport, backupDirectory, backedUpFiles, progress);
                PreparePostProcessorRollback(
                    filesToImport,
                    locations,
                    backupDirectory,
                    backedUpFiles,
                    newlyCreatedFiles);
                PrepareSqliteSnapshotCompanionRemoval(
                    filesToImport,
                    backupDirectory,
                    backedUpFiles);

                var fileCount = 0;
                long totalBytes = 0;
                foreach (var (entry, targetPath) in filesToImport)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report($"Importiere: {entry.Name}");

                    var existedBefore = File.Exists(targetPath);
                    var directory = Path.GetDirectoryName(targetPath);
                    if (directory is not null)
                        Directory.CreateDirectory(directory);

                    await CopyArchiveEntryAtomicallyAsync(entry, targetPath, ct).ConfigureAwait(false);
                    if (!existedBefore)
                        newlyCreatedFiles.Add(targetPath);

                    fileCount++;
                    totalBytes += entry.Length;
                }

                await KnowledgeBackupImportPostProcessor.ApplyAsync(locations, progress, ct)
                    .ConfigureAwait(false);
                SafeDeleteBackupDirectory(backupDirectory);

                progress?.Report($"Import abgeschlossen: {fileCount} Dateien");
                return new BackupResult(true, null, fileCount, totalBytes);
            }
            catch (Exception)
            {
                RollBack(backedUpFiles, newlyCreatedFiles, backupDirectory, progress);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[KnowledgeBackup] Import fehlgeschlagen ({zipPath}): {ex.GetType().Name}: {ex.Message}");
            return new BackupResult(false, UserError.Describe(ex), 0, 0);
        }
    }

    internal static void FlushSqliteWal(IProgress<string>? progress)
    {
        try
        {
            var dbPath = AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBasePaths.GetKnowledgeDbPath();
            if (!File.Exists(dbPath))
                return;

            progress?.Report("SQLite WAL-Checkpoint...");
            using var context = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
            using var command = context.Connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // Ohne sauberen Checkpoint ist kein konsistenter Stand garantiert.
            // Der Export wird deshalb abgebrochen statt ein unsicheres Archiv zu erzeugen.
            throw new UserFacingException(
                "SQLite WAL-Checkpoint fehlgeschlagen; der Export wurde abgebrochen. " +
                $"Technischer Hinweis: {ex.Message}");
        }
    }

    private static async Task<BackupResult?> CheckManifestAsync(
        ZipArchive zip,
        CancellationToken ct)
    {
        var manifestEntry = zip.GetEntry("_manifest.json");
        if (manifestEntry is null)
            return null;

        await using var stream = manifestEntry.Open();
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct)
            .ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("Version", out var versionProperty))
            return null;

        var version = versionProperty.GetInt32();
        return BackupManifestVersionPolicy.IsCompatible(version)
            ? null
            : new BackupResult(
                false,
                BackupManifestVersionPolicy.FormatIncompatibleMessage(version),
                0,
                0);
    }

    private static List<(ZipArchiveEntry Entry, string TargetPath)> CollectImportFiles(
        ZipArchive zip,
        KnowledgeBackupLocations locations)
    {
        var files = new List<(ZipArchiveEntry, string)>();
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName == "_manifest.json" || string.IsNullOrEmpty(entry.Name))
                continue;

            var targetPath = KnowledgeBackupFileCatalog.MapEntryToLocalPath(entry.FullName, locations);
            if (targetPath is not null)
                files.Add((entry, targetPath));
        }

        return files;
    }

    private static void BackupExistingFiles(
        IEnumerable<(ZipArchiveEntry Entry, string TargetPath)> files,
        string backupDirectory,
        ICollection<(string Original, string Backup)> backedUpFiles,
        IProgress<string>? progress)
    {
        progress?.Report("Sichere bestehende Daten...");
        foreach (var (_, targetPath) in files)
        {
            if (!File.Exists(targetPath))
                continue;

            var backupPath = Path.Combine(backupDirectory, GetRelativeBackupPath(targetPath));
            var directory = Path.GetDirectoryName(backupPath);
            if (directory is not null)
                Directory.CreateDirectory(directory);
            File.Copy(targetPath, backupPath, overwrite: true);
            backedUpFiles.Add((targetPath, backupPath));
        }
    }

    private static void PreparePostProcessorRollback(
        IEnumerable<(ZipArchiveEntry Entry, string TargetPath)> files,
        KnowledgeBackupLocations locations,
        string backupDirectory,
        ICollection<(string Original, string Backup)> backedUpFiles,
        ICollection<string> newlyCreatedFiles)
    {
        var importsTrainingCenterState = false;
        foreach (var (entry, _) in files)
        {
            if (!string.Equals(
                    entry.FullName,
                    "knowledge/training_center.json",
                    StringComparison.OrdinalIgnoreCase))
                continue;

            importsTrainingCenterState = true;
            break;
        }

        if (!importsTrainingCenterState)
            return;

        var targetPath = locations.TrainingCenterStatePath;
        if (!File.Exists(targetPath))
        {
            newlyCreatedFiles.Add(targetPath);
            return;
        }

        var backupPath = Path.Combine(backupDirectory, GetRelativeBackupPath(targetPath));
        var directory = Path.GetDirectoryName(backupPath);
        if (directory is not null)
            Directory.CreateDirectory(directory);
        File.Copy(targetPath, backupPath, overwrite: true);
        backedUpFiles.Add((targetPath, backupPath));
    }

    /// <summary>
    /// Neue Archive enthalten die Wissensdatenbank als eigenstaendigen Snapshot
    /// ohne WAL-/SHM-Begleiter. Veraltete lokale Begleiter wuerden beim naechsten
    /// Start gegen die wiederhergestellte Datei laufen und werden deshalb
    /// rollback-sicher entfernt. Archive des alten Formats importieren ihre
    /// Begleitdateien weiterhin direkt.
    /// </summary>
    private static void PrepareSqliteSnapshotCompanionRemoval(
        IReadOnlyList<(ZipArchiveEntry Entry, string TargetPath)> files,
        string backupDirectory,
        ICollection<(string Original, string Backup)> backedUpFiles)
    {
        string? databaseTargetPath = null;
        var importsWalCompanion = false;
        foreach (var (entry, targetPath) in files)
        {
            if (string.Equals(
                    entry.FullName,
                    KnowledgeDatabaseEntryName,
                    StringComparison.OrdinalIgnoreCase))
            {
                databaseTargetPath = targetPath;
            }
            else if (string.Equals(
                         entry.FullName,
                         KnowledgeDatabaseEntryName + "-wal",
                         StringComparison.OrdinalIgnoreCase))
            {
                importsWalCompanion = true;
            }
        }

        if (databaseTargetPath is null || importsWalCompanion)
            return;

        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var companionPath = databaseTargetPath + suffix;
            if (!File.Exists(companionPath))
                continue;

            var backupPath = Path.Combine(backupDirectory, GetRelativeBackupPath(companionPath));
            var directory = Path.GetDirectoryName(backupPath);
            if (directory is not null)
                Directory.CreateDirectory(directory);
            File.Copy(companionPath, backupPath, overwrite: true);
            backedUpFiles.Add((companionPath, backupPath));
            File.Delete(companionPath);
        }
    }

    private static void RollBack(
        IEnumerable<(string Original, string Backup)> backedUpFiles,
        IEnumerable<string> newlyCreatedFiles,
        string backupDirectory,
        IProgress<string>? progress)
    {
        progress?.Report("Fehler beim Import - stelle vorherigen Zustand wieder her...");
        foreach (var (original, backup) in backedUpFiles)
        {
            BestEffort.Try(
                () => File.Copy(backup, original, overwrite: true),
                $"Knowledge-Import-Rollback: {original} wiederherstellen");
        }

        foreach (var newFile in newlyCreatedFiles)
        {
            BestEffort.Try(
                () => File.Delete(newFile),
                $"Knowledge-Import-Rollback: neue Datei {newFile} loeschen");
        }

        SafeDeleteBackupDirectory(backupDirectory);
    }

    private static async Task CopyArchiveEntryAtomicallyAsync(
        ZipArchiveEntry entry,
        string targetPath,
        CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException($"Zielordner fehlt: {targetPath}");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var source = entry.Open())
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await source.CopyToAsync(destination, ct).ConfigureAwait(false);
                await destination.FlushAsync(ct).ConfigureAwait(false);
            }

            if (File.Exists(targetPath))
                File.Replace(temporaryPath, targetPath, null, ignoreMetadataErrors: true);
            else
                File.Move(temporaryPath, targetPath);
        }
        finally
        {
            BestEffort.Try(
                () =>
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                },
                $"Knowledge-Import: Temp-Datei {temporaryPath} loeschen");
        }
    }

    private static void CommitArchive(string temporaryPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
            File.Replace(temporaryPath, destinationPath, null, ignoreMetadataErrors: true);
        else
            File.Move(temporaryPath, destinationPath);
    }

    private static void SafeDeleteBackupDirectory(string directory)
    {
        try
        {
            if (!InfraBackup.SafePathGuard.IsSafeToDelete(directory))
            {
                BestEffort.ReportWarning(
                    $"[KnowledgeBackup] Verzeichnis-Loeschung abgelehnt: {directory}");
                return;
            }

            System.Diagnostics.Trace.WriteLine(
                $"[KnowledgeBackup] Loesche Backup-Verzeichnis: {directory}");
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[KnowledgeBackup] Fehler beim Loeschen von {directory}: {ex.Message}");
        }
    }

    private static string GetRelativeBackupPath(string fullPath)
    {
        var pathHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(fullPath))));
        return Path.Combine(pathHash, Path.GetFileName(fullPath));
    }
}
