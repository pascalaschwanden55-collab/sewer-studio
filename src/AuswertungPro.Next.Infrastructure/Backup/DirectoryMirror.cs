using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Inkrementeller Verzeichnis-Spiegel der Datensicherung.
/// Kopiert nur fehlende/geaenderte Dateien (Groesse ODER LastWriteTimeUtc-Differenz
/// ueber 2 s — FAT32/exFAT-USB-Ziele haben 2-Sekunden-Granularitaet) und loescht
/// im Ziel Verwaistes. Die Quelle wird NIE beschrieben oder geloescht.
/// </summary>
public sealed class DirectoryMirror
{
    /// <summary>Halbfertige Kopien tragen dieses Suffix und werden nie als "aktuell" gewertet.</summary>
    public const string TempSuffix = ".tmp_sewerbackup";

    private static readonly TimeSpan TimestampToleranz = TimeSpan.FromSeconds(2);

    /// <summary>Laufende Zaehler eines Spiegel-Laufs (ueber alle Quellen geteilt).</summary>
    public sealed class MirrorStats
    {
        public long BytesCopied;
        public int Copied;
        public int Unchanged;
        public int Deleted;
        /// <summary>Format "pfad: grund" — Fehler brechen den Lauf nicht ab.</summary>
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// Spiegelt eine Quelle in den Spiegel-Root. Alle erwarteten Ziel-Relativpfade
    /// landen in <paramref name="expectedTargets"/> (Basis der Verwaisten-Loeschung).
    /// </summary>
    /// <param name="onFileDone">Wird pro Datei gerufen (auch fuer Unveraendertes) — fuer Byte-Fortschritt.</param>
    public async Task MirrorSourceAsync(
        BackupSource source,
        string backupRoot,
        ISet<string> expectedTargets,
        MirrorStats stats,
        Action<string, long>? onFileDone = null,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(source.SourceRoot))
            return;

        foreach (var file in EnumerateFiles(source.SourceRoot, source.IsDirExcluded, stats))
        {
            ct.ThrowIfCancellationRequested();

            var relToSource = Path.GetRelativePath(source.SourceRoot, file);
            var targetRel = Path.Combine(source.TargetRelativeRoot, relToSource);
            expectedTargets.Add(targetRel);

            await CopyIfChangedAsync(file, Path.Combine(backupRoot, targetRel), stats, onFileDone, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Spiegelt eine Einzeldatei (z. B. Desktop-Skript). Fehlende Quelle wird still uebersprungen.</summary>
    public async Task MirrorFileAsync(
        BackupSingleFile file,
        string backupRoot,
        ISet<string> expectedTargets,
        MirrorStats stats,
        Action<string, long>? onFileDone = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(file.SourcePath))
            return;

        expectedTargets.Add(file.TargetRelativePath);
        await CopyIfChangedAsync(file.SourcePath, Path.Combine(backupRoot, file.TargetRelativePath),
            stats, onFileDone, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Loescht Dateien und leere Ordner im Spiegel-Root, die nicht erwartet sind.
    /// NUR aufrufen wenn der Marker verifiziert wurde; zusaetzlich wird jeder
    /// Loeschpfad gegen den Spiegel-Root geprueft (Defense-in-Depth).
    /// </summary>
    public void DeleteOrphans(string backupRoot, ISet<string> expectedTargets, MirrorStats stats)
    {
        foreach (var file in EnumerateFiles(backupRoot, isDirExcluded: null, stats))
        {
            var rel = Path.GetRelativePath(backupRoot, file);
            if (expectedTargets.Contains(rel))
                continue;
            if (!BackupTargetGuard.IsInsideBackupRoot(backupRoot, file))
                continue;

            try
            {
                File.Delete(file);
                stats.Deleted++;
            }
            catch (Exception ex)
            {
                stats.Errors.Add($"{file}: Loeschen fehlgeschlagen ({ex.Message})");
            }
        }

        DeleteEmptyDirectories(backupRoot, stats);
    }

    private static async Task CopyIfChangedAsync(
        string sourceFile,
        string targetFile,
        MirrorStats stats,
        Action<string, long>? onFileDone,
        CancellationToken ct)
    {
        try
        {
            var sourceInfo = new FileInfo(sourceFile);
            var targetInfo = new FileInfo(targetFile);

            if (targetInfo.Exists
                && targetInfo.Length == sourceInfo.Length
                && (targetInfo.LastWriteTimeUtc - sourceInfo.LastWriteTimeUtc).Duration() <= TimestampToleranz)
            {
                stats.Unchanged++;
                onFileDone?.Invoke(sourceFile, sourceInfo.Length);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

            // Erst in Temp-Datei schreiben, dann atomar umbenennen — halbfertige
            // Kopien (Absturz/Abbruch) werden so nie faelschlich als aktuell gewertet.
            var tempFile = targetFile + TempSuffix;
            try
            {
                using (var src = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var dst = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await src.CopyToAsync(dst, ct).ConfigureAwait(false);
                }

                File.SetLastWriteTimeUtc(tempFile, sourceInfo.LastWriteTimeUtc);
                File.Move(tempFile, targetFile, overwrite: true);
            }
            catch
            {
                TryDeleteTemp(tempFile);
                throw;
            }

            stats.Copied++;
            stats.BytesCopied += sourceInfo.Length;
            onFileDone?.Invoke(sourceFile, sourceInfo.Length);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException or NotSupportedException)
        {
            stats.Errors.Add($"{sourceFile}: {ex.Message}");
        }
    }

    /// <summary>
    /// Rekursive Datei-Enumeration, die ausgeschlossene Ordner gar nicht erst betritt
    /// (Muster SafeFileEnumeration: Stack-basiert, Fehler pro Ordner abgefangen).
    /// Das Praedikat bekommt den Ordnerpfad relativ zum Root.
    /// </summary>
    private static IEnumerable<string> EnumerateFiles(
        string root,
        Func<string, bool>? isDirExcluded,
        MirrorStats stats)
    {
        if (!Directory.Exists(root))
            yield break;

        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            string[] files;
            string[] children;
            try
            {
                files = Directory.EnumerateFiles(current)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                children = Directory.EnumerateDirectories(current)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                stats.Errors.Add($"{current}: Ordner nicht lesbar ({ex.Message})");
                continue;
            }

            foreach (var file in files)
                yield return file;

            for (var i = children.Length - 1; i >= 0; i--)
            {
                var relDir = Path.GetRelativePath(root, children[i]);
                if (isDirExcluded is null || !isDirExcluded(relDir))
                    stack.Push(children[i]);
            }
        }
    }

    private static void DeleteEmptyDirectories(string backupRoot, MirrorStats stats)
    {
        // Tiefste Ordner zuerst, damit Ketten leerer Ordner komplett verschwinden.
        List<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(backupRoot, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stats.Errors.Add($"{backupRoot}: Ordner-Aufraeumen fehlgeschlagen ({ex.Message})");
            return;
        }

        foreach (var dir in dirs)
        {
            if (!BackupTargetGuard.IsInsideBackupRoot(backupRoot, dir))
                continue;

            try
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch (IOException)
            {
                // Nicht leer oder gesperrt — stehen lassen.
            }
            catch (UnauthorizedAccessException)
            {
                // Stehen lassen.
            }
        }
    }

    private static void TryDeleteTemp(string tempFile)
    {
        try
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
        catch
        {
            // Best effort — Reste raeumt die Verwaisten-Loeschung des Folgelaufs ab.
        }
    }
}
