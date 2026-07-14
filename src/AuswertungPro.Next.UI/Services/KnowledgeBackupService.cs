using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Ai.Backup;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using InfraKnowledgeBase = AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;
using InfraBackup = AuswertungPro.Next.Infrastructure.Ai.Backup;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Export/Import aller KI-Lerndaten und Einstellungen als ZIP-Archiv.
/// Vollstaendiger "Hirntransfer" — alle Artefakte aus KnowledgeRoot werden gesichert.
/// </summary>
public static class KnowledgeBackupService
{
    public sealed record BackupResult(bool Success, string? Error, int FileCount, long SizeBytes);

    /// <summary>Aktuelle Manifest-Version. Wird beim Export geschrieben und beim Import geprueft.</summary>
    private const int ManifestVersion = BackupManifestVersionPolicy.CurrentVersion;

    // ── Export ────────────────────────────────────────────────────────

    public static async Task<BackupResult> ExportAsync(
        string zipPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            AppSettings.FlushPendingSave();

            // SQLite WAL-Checkpoint: Alle Daten in die Hauptdatei flushen
            // damit der Export transaktionskonsistent ist.
            FlushSqliteWal(progress);

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
                    // FileShare.ReadWrite: SQLite DB kann noch offen sein (nach Checkpoint ok)
                    using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    await src.CopyToAsync(dest, ct).ConfigureAwait(false);
                    fileCount++;
                }

                // Manifest mit Version + Pruefsumme
                var manifest = new
                {
                    Version = ManifestVersion,
                    Product = "SewerStudio",
                    ExportedUtc = DateTime.UtcNow.ToString("o"),
                    FileCount = fileCount,
                    KnowledgeRoot = InfraKnowledgeBase.KnowledgeBasePaths.GetRoot()
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
            BestEffort.ReportWarning(
                $"[KnowledgeBackup] Export fehlgeschlagen ({zipPath}): {ex.GetType().Name}: {ex.Message}");
            return new BackupResult(false, UserError.Describe(ex), 0, 0);
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

            using var zip = ZipFile.OpenRead(zipPath);

            // Manifest-Version pruefen
            var manifestEntry = zip.GetEntry("_manifest.json");
            if (manifestEntry is not null)
            {
                using var ms = manifestEntry.Open();
                var doc = await JsonDocument.ParseAsync(ms, cancellationToken: ct).ConfigureAwait(false);
                if (doc.RootElement.TryGetProperty("Version", out var vProp)
                    && !BackupManifestVersionPolicy.IsCompatible(vProp.GetInt32()))
                {
                    return new BackupResult(false,
                        BackupManifestVersionPolicy.FormatIncompatibleMessage(vProp.GetInt32()),
                        0, 0);
                }
            }

            // Sammle alle Ziel-Pfade fuer atomaren Import
            var filesToImport = new List<(ZipArchiveEntry Entry, string TargetPath)>();
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName == "_manifest.json" || string.IsNullOrEmpty(entry.Name))
                    continue;
                var targetPath = MapEntryToLocalPath(entry.FullName);
                if (targetPath is not null)
                    filesToImport.Add((entry, targetPath));
            }

            if (filesToImport.Count == 0)
                return new BackupResult(false, "Keine importierbaren Dateien im Archiv gefunden.", 0, 0);

            // Backup bestehender Dateien in Temp-Ordner (fuer Rollback)
            var backupDir = Path.Combine(Path.GetTempPath(), $"sewerstudio_import_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(backupDir);
            var backedUpFiles = new List<(string Original, string Backup)>();
            var newlyCreatedFiles = new List<string>(); // Dateien die vorher nicht existierten

            try
            {
                // Phase 1: Bestehende Dateien sichern
                progress?.Report("Sichere bestehende Daten...");
                foreach (var (_, targetPath) in filesToImport)
                {
                    if (File.Exists(targetPath))
                    {
                        var relPath = GetRelativeBackupPath(targetPath);
                        var backupPath = Path.Combine(backupDir, relPath);
                        var backupFileDir = Path.GetDirectoryName(backupPath);
                        if (backupFileDir is not null)
                            Directory.CreateDirectory(backupFileDir);
                        File.Copy(targetPath, backupPath, overwrite: true);
                        backedUpFiles.Add((targetPath, backupPath));
                    }
                }

                // Phase 2: Dateien importieren
                int fileCount = 0;
                long totalBytes = 0;
                foreach (var (entry, targetPath) in filesToImport)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report($"Importiere: {entry.Name}");

                    var existedBefore = File.Exists(targetPath);
                    var dir = Path.GetDirectoryName(targetPath);
                    if (dir is not null)
                        Directory.CreateDirectory(dir);

                    await CopyArchiveEntryAtomicallyAsync(entry, targetPath, ct).ConfigureAwait(false);

                    if (!existedBefore)
                        newlyCreatedFiles.Add(targetPath);

                    fileCount++;
                    totalBytes += entry.Length;
                }

                // Phase 3: FramePaths in training_samples.json remappen
                progress?.Report("Passe Frame-Pfade an lokale Struktur an...");
                await RemapFramePathsAsync(ct).ConfigureAwait(false);

                // Phase 4: training_center.json aus Knowledge-Root an den
                // aktuellen AppData-Pfad kopieren (TrainingCenterStore liest von dort).
                CopyTrainingCenterStateToAppData();

                // Phase 5: Absolute Pfade in teacher_annotations.json remappen
                progress?.Report("Passe Lehrer-Annotationspfade an...");
                RemapTeacherAnnotationPaths();

                // Erfolg — Backup-Ordner aufraeumen
                SafeDeleteBackupDir(backupDir);

                progress?.Report($"Import abgeschlossen: {fileCount} Dateien");
                return new BackupResult(true, null, fileCount, totalBytes);
            }
            catch (Exception)
            {
                // Rollback: Gesicherte Dateien wiederherstellen + neue Dateien loeschen
                progress?.Report("Fehler beim Import — stelle vorherigen Zustand wieder her...");
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
                SafeDeleteBackupDir(backupDir);
                throw;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[KnowledgeBackup] Import fehlgeschlagen ({zipPath}): {ex.GetType().Name}: {ex.Message}");
            return new BackupResult(false, UserError.Describe(ex), 0, 0);
        }
    }

    // ── SQLite WAL-Checkpoint ─────────────────────────────────────────

    /// <summary>
    /// Flusht alle WAL-Eintraege in die SQLite-Hauptdatei.
    /// Garantiert transaktionskonsistenten Export.
    /// </summary>
    private static void FlushSqliteWal(IProgress<string>? progress)
    {
        try
        {
            var dbPath = InfraKnowledgeBase.KnowledgeBasePaths.GetKnowledgeDbPath();
            if (!File.Exists(dbPath)) return;

            progress?.Report("SQLite WAL-Checkpoint...");
            using var ctx = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
            using var cmd = ctx.Connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[KnowledgeBackup] WAL-Checkpoint fehlgeschlagen: {ex.Message}");
            // Export fortsetzen — WAL/SHM werden mitgenommen als Fallback
        }
    }

    // ── Safe Directory Delete ───────────────────────────────────────────

    /// <summary>
    /// Loescht ein Backup-Verzeichnis nur wenn der Pfad valide ist und "backup" im Namen enthaelt.
    /// Delegiert Sicherheitspruefung an SafePathGuard (Infrastructure-Schicht).
    /// </summary>
    private static void SafeDeleteBackupDir(string dirPath)
    {
        try
        {
            if (!InfraBackup.SafePathGuard.IsSafeToDelete(dirPath))
            {
                BestEffort.ReportWarning(
                    $"[KnowledgeBackup] Verzeichnis-Loeschung abgelehnt: {dirPath}");
                return;
            }

            System.Diagnostics.Trace.WriteLine($"[KnowledgeBackup] Loesche Backup-Verzeichnis: {dirPath}");
            Directory.Delete(dirPath, recursive: true);
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[KnowledgeBackup] Fehler beim Loeschen von {dirPath}: {ex.Message}");
        }
    }

    // ── FramePath-Remapping ───────────────────────────────────────────

    /// <summary>
    /// Passt absolute FramePaths in training_samples.json an den lokalen KnowledgeRoot an.
    /// Erkennt Frames die im knowledge/frames/ Ordner liegen und setzt den lokalen Pfad.
    /// </summary>
    private static async Task RemapFramePathsAsync(CancellationToken ct)
    {
        try
        {
            var samplesPath = InfraKnowledgeBase.KnowledgeBasePaths.GetTrainingSamplesPath();
            if (!File.Exists(samplesPath)) return;

            var json = await File.ReadAllTextAsync(samplesPath, ct).ConfigureAwait(false);
            var samples = JsonSerializer.Deserialize<List<TrainingSample>>(json);
            if (samples is null || samples.Count == 0) return;

            var localFramesDir = InfraKnowledgeBase.KnowledgeBasePaths.GetFramesDir();
            var changed = false;

            foreach (var s in samples)
            {
                if (string.IsNullOrEmpty(s.FramePath)) continue;

                // Fall 1: FramePath ist absolut und zeigt auf fremden Rechner
                // → pruefen ob die Datei lokal im frames/ Ordner liegt
                var remapped = FramePathRemapper.RemapFramePath(s.FramePath, localFramesDir, File.Exists);
                if (remapped is not null)
                {
                    s.FramePath = remapped;
                    changed = true;
                }

                // Fall 2: AdditionalFramePaths remappen
                if (s.AdditionalFramePaths is { Count: > 0 })
                {
                    for (var i = 0; i < s.AdditionalFramePaths.Count; i++)
                    {
                        var afRemapped = FramePathRemapper.RemapFramePath(s.AdditionalFramePaths[i], localFramesDir, File.Exists);
                        if (afRemapped is not null)
                        {
                            s.AdditionalFramePaths[i] = afRemapped;
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var newJson = JsonSerializer.Serialize(samples, opts);
                await AtomicTextFileWriter.WriteAllTextAsync(samplesPath, newJson, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            throw new IOException("Frame-Pfade konnten nicht sicher gespeichert werden.", ex);
        }
    }

    // ── TrainingCenter-State Sync ────────────────────────────────────

    /// <summary>
    /// Kopiert training_center.json aus dem Knowledge-Root nach AppData,
    /// damit der TrainingCenterStore den importierten Fortschritt findet.
    /// </summary>
    private static void CopyTrainingCenterStateToAppData()
    {
        try
        {
            var importedPath = Path.Combine(InfraKnowledgeBase.KnowledgeBasePaths.GetRoot(), "training_center.json");
            if (!File.Exists(importedPath)) return;

            var tcStore = new Ai.Training.TrainingCenterStore();
            var targetPath = tcStore.StoreFilePath;

            // Zielverzeichnis sicherstellen
            var dir = Path.GetDirectoryName(targetPath);
            if (dir is not null) Directory.CreateDirectory(dir);

            AtomicTextFileWriter.WriteAllText(targetPath, File.ReadAllText(importedPath));
            System.Diagnostics.Trace.WriteLine($"[KnowledgeBackup] training_center.json → {targetPath}");
        }
        catch (Exception ex)
        {
            throw new IOException("Training-Center-Stand konnte nicht sicher gespeichert werden.", ex);
        }
    }

    // ── Teacher-Annotations Pfad-Remapping ──────────────────────────

    /// <summary>
    /// Passt absolute Pfade (FullFramePath, CroppedRegionPath, YoloAnnotationPath)
    /// in teacher_annotations.json an den lokalen KnowledgeRoot an.
    /// Beim Transfer auf einen anderen Rechner zeigen die Pfade sonst ins Leere.
    /// </summary>
    private static void RemapTeacherAnnotationPaths()
    {
        try
        {
            var annotationsPath = Path.Combine(InfraKnowledgeBase.KnowledgeBasePaths.GetRoot(), "teacher_annotations.json");
            if (!File.Exists(annotationsPath)) return;

            var json = File.ReadAllText(annotationsPath);
            var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
            var annotations = JsonSerializer.Deserialize<List<TeacherAnnotation>>(json, opts);
            if (annotations is null || annotations.Count == 0) return;

            var localImagesDir = InfraTeacher.TeacherAnnotationStore.GetImagesDir();
            var localLabelsDir = InfraTeacher.TeacherAnnotationStore.GetLabelsDir();
            var changed = false;

            foreach (var a in annotations)
            {
                var p1 = FramePathRemapper.RemapPathToLocal(a.FullFramePath, localImagesDir, File.Exists);
                if (p1 is not null) { a.FullFramePath = p1; changed = true; }

                var p2 = FramePathRemapper.RemapPathToLocal(a.CroppedRegionPath, localImagesDir, File.Exists);
                if (p2 is not null) { a.CroppedRegionPath = p2; changed = true; }

                var p3 = FramePathRemapper.RemapPathToLocal(a.YoloAnnotationPath, localLabelsDir, File.Exists);
                if (p3 is not null) { a.YoloAnnotationPath = p3; changed = true; }
            }

            if (changed)
            {
                var newJson = JsonSerializer.Serialize(annotations, opts);
                AtomicTextFileWriter.WriteAllText(annotationsPath, newJson);
                System.Diagnostics.Trace.WriteLine(
                    $"[KnowledgeBackup] Teacher-Annotationen remapped: {annotations.Count} Eintraege");
            }
        }
        catch (Exception ex)
        {
            throw new IOException("Lehrer-Annotationen konnten nicht sicher gespeichert werden.", ex);
        }
    }

    private static async Task CopyArchiveEntryAtomicallyAsync(
        ZipArchiveEntry entry,
        string targetPath,
        CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException($"Zielordner fehlt: {targetPath}");
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var source = entry.Open())
            await using (var destination = new FileStream(
                tempPath,
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
                File.Replace(tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else
                File.Move(tempPath, targetPath);
        }
        finally
        {
            BestEffort.Try(
                () => { if (File.Exists(tempPath)) File.Delete(tempPath); },
                $"Knowledge-Import: Temp-Datei {tempPath} loeschen");
        }
    }

    // ── Path helpers ─────────────────────────────────────────────────

    private static readonly string RoamingAp = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuswertungPro");

    private static readonly string RoamingSs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppIdentity.ProductName);

    private static readonly string LocalSs = AppSettings.AppDataDir;

    private static IEnumerable<(string Source, string Entry)> EnumerateBackupFiles()
    {
        var knowledgeRoot = InfraKnowledgeBase.KnowledgeBasePaths.GetRoot();

        // ══════════════════════════════════════════════════════════════════
        // KNOWLEDGE-ROOT: Alle KI-Artefakte (vollstaendiger Hirntransfer)
        // ══════════════════════════════════════════════════════════════════

        // KB-Datenbank (nach WAL-Checkpoint nur noch .db noetig, WAL/SHM als Sicherheit)
        var kbDbPath = InfraKnowledgeBase.KnowledgeBasePaths.GetKnowledgeDbPath();
        yield return (kbDbPath, "knowledge/KnowledgeBase.db");
        yield return (kbDbPath + "-wal", "knowledge/KnowledgeBase.db-wal");
        yield return (kbDbPath + "-shm", "knowledge/KnowledgeBase.db-shm");

        // Training Samples + Settings
        yield return (InfraKnowledgeBase.KnowledgeBasePaths.GetTrainingSamplesPath(), "knowledge/training_samples.json");
        yield return (InfraKnowledgeBase.KnowledgeBasePaths.GetTrainingSettingsPath(), "knowledge/training_settings.json");

        // Frames (extrahierte Video-Bilder)
        var knowledgeFramesDir = InfraKnowledgeBase.KnowledgeBasePaths.GetFramesDir();
        if (Directory.Exists(knowledgeFramesDir))
        {
            foreach (var png in Directory.EnumerateFiles(knowledgeFramesDir, "*.png"))
                yield return (png, "knowledge/frames/" + Path.GetFileName(png));
        }

        // Fachauge-kuratierte Gold-Labels fuer Klassifikator-Training.
        var goldLabelsDir = Path.Combine(knowledgeRoot, "gold_labels");
        if (Directory.Exists(goldLabelsDir))
        {
            foreach (var file in AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(goldLabelsDir, "*.*", recursive: true))
            {
                var relPath = Path.GetRelativePath(goldLabelsDir, file).Replace('\\', '/');
                yield return (file, "knowledge/gold_labels/" + relPath);
            }
        }

        // Few-Shot-Bibliothek (Qwen-Beispiele)
        yield return (Path.Combine(knowledgeRoot, "fewshot_examples.json"), "knowledge/fewshot_examples.json");
        var fewshotImagesDir = Path.Combine(knowledgeRoot, "fewshot_images");
        if (Directory.Exists(fewshotImagesDir))
        {
            foreach (var img in Directory.EnumerateFiles(fewshotImagesDir, "*.*"))
                yield return (img, "knowledge/fewshot_images/" + Path.GetFileName(img));
        }

        // Lehrer-Annotationen (YOLO-Training)
        yield return (Path.Combine(knowledgeRoot, "teacher_annotations.json"), "knowledge/teacher_annotations.json");
        var teacherImagesDir = Path.Combine(knowledgeRoot, "teacher_images");
        if (Directory.Exists(teacherImagesDir))
        {
            // AllDirectories: auch crops/ Unterordner exportieren
            foreach (var img in AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(teacherImagesDir, "*.*", recursive: true))
            {
                var relPath = Path.GetRelativePath(teacherImagesDir, img).Replace('\\', '/');
                yield return (img, "knowledge/teacher_images/" + relPath);
            }
        }
        var teacherLabelsDir = Path.Combine(knowledgeRoot, "teacher_labels");
        if (Directory.Exists(teacherLabelsDir))
        {
            foreach (var txt in AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(teacherLabelsDir, "*.txt", recursive: true))
            {
                var relPath = Path.GetRelativePath(teacherLabelsDir, txt).Replace('\\', '/');
                yield return (txt, "knowledge/teacher_labels/" + relPath);
            }
        }

        // YOLO-Klassenmapping
        yield return (Path.Combine(knowledgeRoot, "yolo_class_map.json"), "knowledge/yolo_class_map.json");
        yield return (Path.Combine(knowledgeRoot, "classes.txt"), "knowledge/classes.txt");

        // Self-Training-Historie
        yield return (Path.Combine(knowledgeRoot, "selftraining_history.json"), "knowledge/selftraining_history.json");

        // Massnahmen-Modell
        yield return (InfraKnowledgeBase.KnowledgeBasePaths.GetMeasuresLearningPath(), "knowledge/measures_learning.json");
        yield return (InfraKnowledgeBase.KnowledgeBasePaths.GetMeasuresModelPath(), "knowledge/measures-model.zip");

        // Training-Center State (Case-Fortschritt) — liegt aktuell in AppData,
        // wird hier zusaetzlich unter knowledge/ exportiert fuer portablen Transfer.
        var tcStore = new Ai.Training.TrainingCenterStore();
        yield return (tcStore.StoreFilePath, "knowledge/training_center.json");

        // ══════════════════════════════════════════════════════════════════
        // LEGACY-PFADE (AppData, fuer Abwaertskompatibilitaet)
        // ══════════════════════════════════════════════════════════════════

        var kbDir = Path.Combine(RoamingAp, "KiVideoanalyse");
        yield return (Path.Combine(kbDir, "KnowledgeBase.db"), "roaming_auswertungpro/KiVideoanalyse/KnowledgeBase.db");
        yield return (Path.Combine(kbDir, "KnowledgeBase.db-wal"), "roaming_auswertungpro/KiVideoanalyse/KnowledgeBase.db-wal");
        yield return (Path.Combine(kbDir, "KnowledgeBase.db-shm"), "roaming_auswertungpro/KiVideoanalyse/KnowledgeBase.db-shm");

        yield return (Path.Combine(RoamingAp, "training_center_samples.json"), "roaming_auswertungpro/training_center_samples.json");
        yield return (Path.Combine(RoamingAp, "training_center_settings.json"), "roaming_auswertungpro/training_center_settings.json");
        yield return (Path.Combine(RoamingAp, "training_center.json"), "roaming_auswertungpro/training_center.json");

        var framesDir = Path.Combine(RoamingAp, "frames");
        if (Directory.Exists(framesDir))
        {
            foreach (var png in Directory.EnumerateFiles(framesDir, "*.png"))
                yield return (png, "roaming_auswertungpro/frames/" + Path.GetFileName(png));
        }

        // SewerStudio dropdowns + presets
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

    /// <summary>
    /// Mappt ZIP-Eintraege zurueck auf lokale Pfade.
    /// Delegiert an KnowledgeBackupPathMapper (Application-Schicht).
    /// </summary>
    private static string? MapEntryToLocalPath(string entryName)
        => KnowledgeBackupPathMapper.MapEntryToLocalPath(
            entryName,
            knowledgeRoot: InfraKnowledgeBase.KnowledgeBasePaths.GetRoot(),
            roamingAp: RoamingAp,
            roamingSs: RoamingSs,
            localSs: LocalSs);

    /// <summary>Erzeugt einen relativen Pfad fuer den Rollback-Ordner.</summary>
    private static string GetRelativeBackupPath(string fullPath)
    {
        // Einfacher Hash des Verzeichnisses + Dateiname
        var dir = Path.GetDirectoryName(fullPath) ?? "";
        var hash = dir.GetHashCode().ToString("X8");
        return Path.Combine(hash, Path.GetFileName(fullPath));
    }
}
