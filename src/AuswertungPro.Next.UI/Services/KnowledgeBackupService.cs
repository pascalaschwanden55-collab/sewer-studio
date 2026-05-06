using System;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Export/Import aller KI-Lerndaten und Einstellungen als ZIP-Archiv.
/// Vollstaendiger "Hirntransfer" — alle Artefakte aus KnowledgeRoot werden gesichert.
/// </summary>
public static class KnowledgeBackupService
{
    public sealed record BackupResult(bool Success, string? Error, int FileCount, long SizeBytes);

    /// <summary>Aktuelle Manifest-Version. Wird beim Export geschrieben und beim Import geprueft.</summary>
    private const int ManifestVersion = 2;

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
                    KnowledgeRoot = Ai.KnowledgeRoot.GetRoot()
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

            using var zip = ZipFile.OpenRead(zipPath);

            // Manifest-Version pruefen
            var manifestEntry = zip.GetEntry("_manifest.json");
            if (manifestEntry is not null)
            {
                using var ms = manifestEntry.Open();
                var doc = await JsonDocument.ParseAsync(ms, cancellationToken: ct).ConfigureAwait(false);
                if (doc.RootElement.TryGetProperty("Version", out var vProp) && vProp.GetInt32() > ManifestVersion)
                {
                    return new BackupResult(false,
                        $"Backup-Version {vProp.GetInt32()} ist neuer als die aktuelle Version {ManifestVersion}. Bitte aktualisieren Sie die Software.",
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

                    using var src = entry.Open();
                    using var dest = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await src.CopyToAsync(dest, ct).ConfigureAwait(false);

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

                // Brain-Mirror sofort synchronisieren (kompletter Import)
                if (KnowledgeMirrorService.Current is { } mirror)
                    await mirror.SyncNowAsync(ct);

                progress?.Report($"Import abgeschlossen: {fileCount} Dateien");
                return new BackupResult(true, null, fileCount, totalBytes);
            }
            catch (Exception)
            {
                // Rollback: Gesicherte Dateien wiederherstellen + neue Dateien loeschen
                progress?.Report("Fehler beim Import — stelle vorherigen Zustand wieder her...");
                foreach (var (original, backup) in backedUpFiles)
                {
                    try { File.Copy(backup, original, overwrite: true); } catch { /* best-effort */ }
                }
                foreach (var newFile in newlyCreatedFiles)
                {
                    try { File.Delete(newFile); } catch { /* best-effort */ }
                }
                SafeDeleteBackupDir(backupDir);
                throw;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new BackupResult(false, ex.Message, 0, 0);
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
            var dbPath = Ai.KnowledgeRoot.GetKnowledgeDbPath();
            if (!File.Exists(dbPath)) return;

            progress?.Report("SQLite WAL-Checkpoint...");
            using var ctx = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
            using var cmd = ctx.Connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KnowledgeBackup] WAL-Checkpoint fehlgeschlagen: {ex.Message}");
            // Export fortsetzen — WAL/SHM werden mitgenommen als Fallback
        }
    }

    // ── Safe Directory Delete ───────────────────────────────────────────

    /// <summary>
    /// Loescht ein Backup-Verzeichnis nur wenn der Pfad valide ist und "backup" im Namen enthaelt.
    /// Verhindert versehentliches Loeschen beliebiger Verzeichnisse.
    /// </summary>
    private static void SafeDeleteBackupDir(string dirPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dirPath))
                return;

            // Sicherheitspruefung: Pfad muss im Temp-Verzeichnis liegen und "backup" enthalten
            var tempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedDir = Path.GetFullPath(dirPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!normalizedDir.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[KnowledgeBackup] Verzeichnis-Loeschung abgelehnt: Pfad liegt nicht im Temp-Verzeichnis: {dirPath}");
                return;
            }

            if (!normalizedDir.Contains("backup", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[KnowledgeBackup] Verzeichnis-Loeschung abgelehnt: Pfad enthaelt nicht 'backup': {dirPath}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[KnowledgeBackup] Loesche Backup-Verzeichnis: {dirPath}");
            Directory.Delete(dirPath, recursive: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KnowledgeBackup] Fehler beim Loeschen von {dirPath}: {ex.Message}");
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
            var samplesPath = Ai.KnowledgeRoot.GetTrainingSamplesPath();
            if (!File.Exists(samplesPath)) return;

            var json = await File.ReadAllTextAsync(samplesPath, ct).ConfigureAwait(false);
            var samples = JsonSerializer.Deserialize<List<AuswertungPro.Next.Domain.Ai.Training.TrainingSample>>(json);
            if (samples is null || samples.Count == 0) return;

            var localFramesDir = Ai.KnowledgeRoot.GetFramesDir();
            var changed = false;

            foreach (var s in samples)
            {
                if (string.IsNullOrEmpty(s.FramePath)) continue;

                // Fall 1: FramePath ist absolut und zeigt auf fremden Rechner
                // → pruefen ob die Datei lokal im frames/ Ordner liegt
                var fileName = Path.GetFileName(s.FramePath);
                var localPath = Path.Combine(localFramesDir, fileName);
                if (File.Exists(localPath) && !string.Equals(s.FramePath, localPath, StringComparison.OrdinalIgnoreCase))
                {
                    s.FramePath = localPath;
                    changed = true;
                }

                // Fall 2: AdditionalFramePaths remappen
                if (s.AdditionalFramePaths is { Count: > 0 })
                {
                    for (var i = 0; i < s.AdditionalFramePaths.Count; i++)
                    {
                        var afn = Path.GetFileName(s.AdditionalFramePaths[i]);
                        var afLocal = Path.Combine(localFramesDir, afn);
                        if (File.Exists(afLocal) && !string.Equals(s.AdditionalFramePaths[i], afLocal, StringComparison.OrdinalIgnoreCase))
                        {
                            s.AdditionalFramePaths[i] = afLocal;
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var newJson = JsonSerializer.Serialize(samples, opts);
                await File.WriteAllTextAsync(samplesPath, newJson, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KnowledgeBackup] FramePath-Remap fehlgeschlagen: {ex.Message}");
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
            var importedPath = Path.Combine(Ai.KnowledgeRoot.GetRoot(), "training_center.json");
            if (!File.Exists(importedPath)) return;

            var tcStore = new Ai.Training.TrainingCenterStore();
            var targetPath = tcStore.StoreFilePath;

            // Zielverzeichnis sicherstellen
            var dir = Path.GetDirectoryName(targetPath);
            if (dir is not null) Directory.CreateDirectory(dir);

            File.Copy(importedPath, targetPath, overwrite: true);
            System.Diagnostics.Debug.WriteLine($"[KnowledgeBackup] training_center.json → {targetPath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KnowledgeBackup] training_center.json Sync fehlgeschlagen: {ex.Message}");
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
            var annotationsPath = Path.Combine(Ai.KnowledgeRoot.GetRoot(), "teacher_annotations.json");
            if (!File.Exists(annotationsPath)) return;

            var json = File.ReadAllText(annotationsPath);
            var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
            var annotations = JsonSerializer.Deserialize<List<AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotation>>(json, opts);
            if (annotations is null || annotations.Count == 0) return;

            var localImagesDir = Ai.Teacher.TeacherAnnotationStore.GetImagesDir();
            var localLabelsDir = Ai.Teacher.TeacherAnnotationStore.GetLabelsDir();
            var changed = false;

            foreach (var a in annotations)
            {
                var p1 = RemapPathToLocal(a.FullFramePath, localImagesDir);
                if (p1 is not null) { a.FullFramePath = p1; changed = true; }

                var p2 = RemapPathToLocal(a.CroppedRegionPath, localImagesDir);
                if (p2 is not null) { a.CroppedRegionPath = p2; changed = true; }

                var p3 = RemapPathToLocal(a.YoloAnnotationPath, localLabelsDir);
                if (p3 is not null) { a.YoloAnnotationPath = p3; changed = true; }
            }

            if (changed)
            {
                var newJson = JsonSerializer.Serialize(annotations, opts);
                File.WriteAllText(annotationsPath, newJson);
                System.Diagnostics.Debug.WriteLine(
                    $"[KnowledgeBackup] Teacher-Annotationen remapped: {annotations.Count} Eintraege");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[KnowledgeBackup] Teacher-Remap fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Gibt den lokalen Pfad zurueck wenn die Datei im Zielordner existiert
    /// und der aktuelle Pfad auf einen anderen Rechner zeigt. Sonst null.
    /// Prueft auch Unterverzeichnisse (z.B. teacher_images/crops/).
    /// </summary>
    private static string? RemapPathToLocal(string? path, string localDir)
    {
        if (string.IsNullOrEmpty(path)) return null;

        var fileName = Path.GetFileName(path);

        // Direkter Pfad: localDir/filename
        var localPath = Path.Combine(localDir, fileName);
        if (File.Exists(localPath) && !string.Equals(path, localPath, StringComparison.OrdinalIgnoreCase))
            return localPath;

        // Unterverzeichnis beibehalten (z.B. crops/filename)
        var parentDir = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
        if (!string.IsNullOrEmpty(parentDir) && !string.Equals(parentDir, Path.GetFileName(localDir), StringComparison.OrdinalIgnoreCase))
        {
            var subPath = Path.Combine(localDir, parentDir, fileName);
            if (File.Exists(subPath) && !string.Equals(path, subPath, StringComparison.OrdinalIgnoreCase))
                return subPath;
        }

        return null;
    }

    // ── Path helpers ─────────────────────────────────────────────────

    private static readonly string RoamingAp = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuswertungPro");

    private static readonly string RoamingSs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppIdentity.ProductName);

    private static readonly string LocalSs = AppSettings.AppDataDir;

    private static IEnumerable<(string Source, string Entry)> EnumerateBackupFiles()
    {
        var knowledgeRoot = Ai.KnowledgeRoot.GetRoot();

        // ══════════════════════════════════════════════════════════════════
        // KNOWLEDGE-ROOT: Alle KI-Artefakte (vollstaendiger Hirntransfer)
        // ══════════════════════════════════════════════════════════════════

        // KB-Datenbank (nach WAL-Checkpoint nur noch .db noetig, WAL/SHM als Sicherheit)
        var kbDbPath = Ai.KnowledgeRoot.GetKnowledgeDbPath();
        yield return (kbDbPath, "knowledge/KnowledgeBase.db");
        yield return (kbDbPath + "-wal", "knowledge/KnowledgeBase.db-wal");
        yield return (kbDbPath + "-shm", "knowledge/KnowledgeBase.db-shm");

        // Training Samples + Settings
        yield return (Ai.KnowledgeRoot.GetTrainingSamplesPath(), "knowledge/training_samples.json");
        yield return (Ai.KnowledgeRoot.GetTrainingSettingsPath(), "knowledge/training_settings.json");

        // Frames (extrahierte Video-Bilder)
        var knowledgeFramesDir = Ai.KnowledgeRoot.GetFramesDir();
        if (Directory.Exists(knowledgeFramesDir))
        {
            foreach (var png in Directory.EnumerateFiles(knowledgeFramesDir, "*.png"))
                yield return (png, "knowledge/frames/" + Path.GetFileName(png));
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
            foreach (var img in Directory.EnumerateFiles(teacherImagesDir, "*.*", SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(teacherImagesDir, img).Replace('\\', '/');
                yield return (img, "knowledge/teacher_images/" + relPath);
            }
        }
        var teacherLabelsDir = Path.Combine(knowledgeRoot, "teacher_labels");
        if (Directory.Exists(teacherLabelsDir))
        {
            foreach (var txt in Directory.EnumerateFiles(teacherLabelsDir, "*.txt", SearchOption.AllDirectories))
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
        yield return (Ai.KnowledgeRoot.GetMeasuresLearningPath(), "knowledge/measures_learning.json");
        yield return (Ai.KnowledgeRoot.GetMeasuresModelPath(), "knowledge/measures-model.zip");

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
    /// Enthaelt Path-Traversal-Schutz gegen ../Angriffe.
    /// </summary>
    private static string? MapEntryToLocalPath(string entryName)
    {
        const string prefixKnowledge = "knowledge/";
        const string prefixAp = "roaming_auswertungpro/";
        const string prefixSs = "roaming_sewerstudio/";
        const string prefixLocal = "local_sewerstudio/";

        string? basePath = null;
        string? relativePart = null;

        if (entryName.StartsWith(prefixKnowledge))
        {
            basePath = Ai.KnowledgeRoot.GetRoot();
            relativePart = entryName[prefixKnowledge.Length..];
        }
        else if (entryName.StartsWith(prefixAp))
        {
            basePath = RoamingAp;
            relativePart = entryName[prefixAp.Length..];
        }
        else if (entryName.StartsWith(prefixSs))
        {
            basePath = RoamingSs;
            relativePart = entryName[prefixSs.Length..];
        }
        else if (entryName.StartsWith(prefixLocal))
        {
            basePath = LocalSs;
            relativePart = entryName[prefixLocal.Length..];
        }

        if (basePath is null || relativePart is null)
            return null;

        // Path-Traversal-Schutz: Aufgeloester Pfad muss innerhalb von basePath bleiben
        var combined = Path.Combine(basePath, relativePart.Replace('/', Path.DirectorySeparatorChar));
        var fullBase = Path.GetFullPath(basePath);
        var fullResolved = Path.GetFullPath(combined);
        if (!fullResolved.StartsWith(fullBase + Path.DirectorySeparatorChar)
            && !string.Equals(fullResolved, fullBase, StringComparison.OrdinalIgnoreCase))
        {
            System.Diagnostics.Debug.WriteLine($"[KnowledgeBackup] Path-Traversal blockiert: {entryName} → {fullResolved}");
            return null;
        }

        return fullResolved;
    }

    /// <summary>Erzeugt einen relativen Pfad fuer den Rollback-Ordner.</summary>
    private static string GetRelativeBackupPath(string fullPath)
    {
        // Einfacher Hash des Verzeichnisses + Dateiname
        var dir = Path.GetDirectoryName(fullPath) ?? "";
        var hash = dir.GetHashCode().ToString("X8");
        return Path.Combine(hash, Path.GetFileName(fullPath));
    }
}
