using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Inkrementeller Verzeichnis-Spiegel der Datensicherung.
/// Kopiert nur fehlende/geaenderte Dateien (Groesse ODER LastWriteTimeUtc-Differenz
/// ueber 2 s — FAT32/exFAT-USB-Ziele haben 2-Sekunden-Granularitaet).
/// Ersetzte und verwaiste Dateien wandern in den datierten Versions-Stand
/// (siehe <see cref="BackupVersionRetention"/>) statt endgueltig zu verschwinden.
/// Die Quelle wird NIE beschrieben oder geloescht.
/// </summary>
public sealed class DirectoryMirror
{
    /// <summary>Halbfertige Kopien tragen dieses Suffix und werden nie als "aktuell" gewertet.</summary>
    public const string TempSuffix = ".tmp_sewerbackup";

    private static readonly TimeSpan TimestampToleranz = TimeSpan.FromSeconds(2);

    private readonly string? _versionsStandName;

    /// <param name="versionsStandName">
    /// Stand-Name dieses Laufs (aus <see cref="BackupVersionRetention.BuildStandName"/>):
    /// ersetzte/verwaiste Dateien wandern nach "_Versionen\{Stand}\...".
    /// null = endgueltig loeschen/ueberschreiben (bewusste Entscheidung des Aufrufers).
    /// </param>
    public DirectoryMirror(string? versionsStandName)
        => _versionsStandName = versionsStandName;

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

            await CopyIfChangedAsync(file, backupRoot, targetRel, stats, onFileDone, ct)
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
        await CopyIfChangedAsync(file.SourcePath, backupRoot, file.TargetRelativePath,
            stats, onFileDone, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Entfernt nicht erwartete Dateien aus dem Spiegel: mit Stand-Name werden sie
    /// nach "_Versionen" verschoben, ohne endgueltig geloescht. Leere Ordner werden
    /// abgeraeumt. Der Versions-Ordner selbst wird nie als verwaist behandelt.
    /// NUR aufrufen wenn der Marker verifiziert wurde; zusaetzlich wird jeder
    /// Pfad gegen den Spiegel-Root geprueft (Defense-in-Depth).
    /// </summary>
    public void RemoveOrphans(string backupRoot, ISet<string> expectedTargets, MirrorStats stats)
    {
        foreach (var file in EnumerateFiles(backupRoot, BackupVersionRetention.IsVersionsDir, stats))
        {
            var rel = Path.GetRelativePath(backupRoot, file);
            if (expectedTargets.Contains(rel))
                continue;
            if (!BackupTargetGuard.IsInsideBackupRoot(backupRoot, file))
                continue;

            try
            {
                if (_versionsStandName is null)
                    File.Delete(file);
                else
                    MoveToVersions(backupRoot, rel, file);
                stats.Deleted++;
            }
            catch (Exception ex)
            {
                stats.Errors.Add($"{file}: Entfernen fehlgeschlagen ({ex.Message})");
            }
        }

        DeleteEmptyDirectories(backupRoot, stats);
    }

    /// <summary>Verschiebt eine Spiegel-Datei in den Stand-Ordner dieses Laufs.</summary>
    private void MoveToVersions(string backupRoot, string targetRel, string file)
    {
        var versionsRel = BackupVersionRetention.BuildVersionsRelativePath(_versionsStandName!, targetRel);
        var versionsPath = Path.Combine(backupRoot, versionsRel);
        Directory.CreateDirectory(Path.GetDirectoryName(versionsPath)!);
        File.Move(file, versionsPath, overwrite: true);
    }

    private async Task CopyIfChangedAsync(
        string sourceFile,
        string backupRoot,
        string targetRel,
        MirrorStats stats,
        Action<string, long>? onFileDone,
        CancellationToken ct)
    {
        var targetFile = Path.Combine(backupRoot, targetRel);
        try
        {
            var sourceInfo = new FileInfo(sourceFile);
            var targetInfo = new FileInfo(targetFile);

            var sameLength = targetInfo.Exists && targetInfo.Length == sourceInfo.Length;
            var timestampDifference = targetInfo.Exists
                ? (targetInfo.LastWriteTimeUtc - sourceInfo.LastWriteTimeUtc).Duration()
                : TimeSpan.MaxValue;
            var unchanged = sameLength && timestampDifference == TimeSpan.Zero;

            // FAT/exFAT runden Zeitstempel auf bis zu zwei Sekunden. Bei einem kleinen,
            // aber echten Unterschied darf gleiche Dateigroesse allein nicht genuegen:
            // SQLite-/JSON-Inhalte koennen sich ohne Groessenaenderung veraendern.
            if (!unchanged
                && sameLength
                && timestampDifference <= TimestampToleranz)
            {
                unchanged = await FilesHaveSameContentAsync(
                        sourceFile,
                        targetFile,
                        sourceInfo.Length,
                        sourceInfo.LastWriteTimeUtc,
                        ct)
                    .ConfigureAwait(false);
            }

            if (unchanged)
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
                TryMoveOldVersionAside(backupRoot, targetRel, targetFile, stats);
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

    private static async Task<bool> FilesHaveSameContentAsync(
        string sourceFile,
        string targetFile,
        long expectedSourceLength,
        DateTime expectedSourceWriteTimeUtc,
        CancellationToken ct)
    {
        byte[] sourceHash;
        await using (var source = new FileStream(
                         sourceFile,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.ReadWrite,
                         bufferSize: 128 * 1024,
                         useAsync: true))
        {
            sourceHash = await SHA256.HashDataAsync(source, ct).ConfigureAwait(false);
        }

        // Wurde die Quelle waehrend der Pruefung veraendert, gilt sie bewusst als
        // geaendert. Der anschliessende Kopierweg versucht dann den aktuellen Stand.
        var sourceAfterHash = new FileInfo(sourceFile);
        if (sourceAfterHash.Length != expectedSourceLength
            || sourceAfterHash.LastWriteTimeUtc != expectedSourceWriteTimeUtc)
            return false;

        byte[] targetHash;
        await using (var target = new FileStream(
                         targetFile,
                         FileMode.Open,
                         FileAccess.Read,
                         FileShare.Read,
                         bufferSize: 128 * 1024,
                         useAsync: true))
        {
            targetHash = await SHA256.HashDataAsync(target, ct).ConfigureAwait(false);
        }

        return sourceHash.AsSpan().SequenceEqual(targetHash);
    }

    /// <summary>
    /// Schiebt die alte Ziel-Version vor dem Ersetzen in den Stand-Ordner (best effort).
    /// Schlaegt das fehl, ersetzt der folgende Move die Datei wie bisher — die
    /// Sicherung bleibt aktuell, nur die Vorversion fehlt (wird protokolliert).
    /// </summary>
    private void TryMoveOldVersionAside(string backupRoot, string targetRel, string targetFile, MirrorStats stats)
    {
        if (_versionsStandName is null || !File.Exists(targetFile))
            return;

        try
        {
            MoveToVersions(backupRoot, targetRel, targetFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException or NotSupportedException)
        {
            stats.Errors.Add($"{targetFile}: Vorversion nicht nach {BackupVersionRetention.VersionsFolderName} verschoben ({ex.Message})");
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
