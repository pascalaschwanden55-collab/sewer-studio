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
    private const int MaxStableCopyAttempts = 3;

    private readonly string? _versionsStandName;
    private readonly Action<string>? _afterTemporaryFileWritten;
    private readonly ISqliteSnapshotCopier _sqliteSnapshots;

    /// <param name="versionsStandName">
    /// Stand-Name dieses Laufs (aus <see cref="BackupVersionRetention.BuildStandName"/>):
    /// ersetzte/verwaiste Dateien wandern nach "_Versionen\{Stand}\...".
    /// null = endgueltig loeschen/ueberschreiben (bewusste Entscheidung des Aufrufers).
    /// </param>
    public DirectoryMirror(string? versionsStandName)
        : this(
            versionsStandName,
            afterTemporaryFileWritten: null,
            sqliteSnapshots: new SqliteSnapshotCopyService())
    {
    }

    internal DirectoryMirror(string? versionsStandName, Action<string>? afterTemporaryFileWritten)
        : this(versionsStandName, afterTemporaryFileWritten, new SqliteSnapshotCopyService())
    {
    }

    internal DirectoryMirror(
        string? versionsStandName,
        Action<string>? afterTemporaryFileWritten,
        ISqliteSnapshotCopier sqliteSnapshots)
    {
        _versionsStandName = versionsStandName;
        _afterTemporaryFileWritten = afterTemporaryFileWritten;
        _sqliteSnapshots = sqliteSnapshots ?? throw new ArgumentNullException(nameof(sqliteSnapshots));
    }

    /// <summary>Laufende Zaehler eines Spiegel-Laufs (ueber alle Quellen geteilt).</summary>
    public sealed class MirrorStats
    {
        public long BytesCopied;
        public int Copied;
        public int Unchanged;
        public int Deleted;
        public int Verified;
        public int DatabasesSnapshotted;
        /// <summary>
        /// Format "pfad: grund". Blockierend: der Aufrufer bricht den Lauf danach ab,
        /// weil der Zielstand sonst unsicher oder unvollstaendig bereinigt wuerde.
        /// </summary>
        public List<string> Errors { get; } = new();
        /// <summary>Nicht-kritische Hinweise, die nach erfolgreichem Lauf sichtbar werden.</summary>
        public List<string> Warnings { get; } = new();

        /// <summary>
        /// Ordnet einen Fehlschlag ein. Nur eine verletzte Zielgrenze
        /// (<see cref="BackupTargetBoundary"/>) bricht die Sicherung ab.
        /// Alles andere — gesperrte Datei, fehlende Rechte, Verknuepfung in der
        /// Quelle — ist eine sichtbare Warnung: Der bisherige Stand dieser Datei
        /// bleibt im Spiegel erhalten, alle uebrigen Dateien werden aktualisiert.
        /// </summary>
        internal void AddIssue(Exception exception, string message)
        {
            if (BackupTargetBoundary.Marks(exception))
                Errors.Add(message);
            else
                Warnings.Add(message);
        }
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
        BackupTargetPathGuard.EnsureRootIsSafe(backupRoot);
        try
        {
            BackupSourcePathGuard.EnsureDirectoryRootIsSafe(source.SourceRoot);
        }
        catch (Exception ex) when (!source.Required
                                   && ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // Optionale Quellen duerfen fehlen. Historische Projekt-Merkeintraege
            // werden sichtbar gemeldet; normale optionale App-Ordner bleiben still.
            if (source.WarnIfMissing)
            {
                stats.Warnings.Add(
                    $"{source.SourceRoot}: Gemerkter Projektordner nicht gefunden - " +
                    "uebersprungen, bisheriger Sicherungsstand bleibt erhalten.");
            }
            PreserveExistingMirror(backupRoot, source.TargetRelativeRoot, expectedTargets);
            return;
        }
        catch (Exception ex) when (ex is FileNotFoundException
                                   or DirectoryNotFoundException
                                   or InvalidDataException
                                   or UnauthorizedAccessException
                                   or IOException)
        {
            // Fehlende Quelle (getrenntes Laufwerk, umbenannter Ordner, falsch gesetzter Root)
            // nicht still uebergehen: sonst raeumt RemoveOrphans den bisherigen Spiegelinhalt
            // dieser Quelle in die Versions-Rotation, waehrend der Lauf Erfolg meldet. Warnung
            // protokollieren UND den vorhandenen Spiegelbestand als "erwartet" markieren.
            stats.Errors.Add(
                $"{source.SourceRoot}: Quellordner nicht sicher lesbar ({ex.Message}) - " +
                "uebersprungen, bisheriger Sicherungsstand bleibt erhalten.");
            PreserveExistingMirror(backupRoot, source.TargetRelativeRoot, expectedTargets);
            return;
        }

        foreach (var file in EnumerateFiles(
                     source.SourceRoot,
                     source.IsDirExcluded,
                     stats,
                     linksAreErrors: false,
                     onSkippedFile: skipped => PreserveSkippedSourceFile(source, skipped, expectedTargets)))
        {
            ct.ThrowIfCancellationRequested();

            var relToSource = Path.GetRelativePath(source.SourceRoot, file);
            if (source.IsFileExcluded?.Invoke(relToSource) == true)
                continue;

            // Eine Online-Sicherung der Hauptdatenbank enthaelt bereits den konsistenten
            // WAL-Stand. Live-WAL/SHM-Dateien duerfen nicht daneben kopiert werden.
            if (_sqliteSnapshots.IsCompanionOfSqliteDatabase(file))
                continue;

            var targetRel = Path.Combine(source.TargetRelativeRoot, relToSource);
            expectedTargets.Add(targetRel);

            await CopyIfChangedAsync(file, backupRoot, targetRel, stats, onFileDone, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Markiert den vorhandenen Spiegelbestand einer (jetzt fehlenden) Quelle als erwartet,
    /// damit <see cref="RemoveOrphans"/> ihn nicht in die Versions-Rotation verschiebt.
    /// </summary>
    /// <summary>
    /// Schuetzt den bisherigen Spiegelstand einer uebersprungenen Quelldatei.
    /// Ohne das wuerde <see cref="RemoveOrphans"/> die letzte gute Kopie als
    /// verwaist behandeln, obwohl die Datei in der Quelle weiterhin existiert.
    /// </summary>
    private static void PreserveSkippedSourceFile(
        BackupSource source, string skippedFile, ISet<string> expectedTargets)
    {
        try
        {
            var relToSource = Path.GetRelativePath(source.SourceRoot, skippedFile);
            expectedTargets.Add(Path.Combine(source.TargetRelativeRoot, relToSource));
        }
        catch (ArgumentException)
        {
            // Ohne bildbaren Zielpfad gibt es nichts zu schuetzen.
        }
    }

    private static void PreserveExistingMirror(
        string backupRoot, string targetRelativeRoot, ISet<string> expectedTargets)
    {
        var existingRoot = Path.Combine(backupRoot, targetRelativeRoot);
        if (!Directory.Exists(existingRoot))
            return;

        foreach (var file in Directory.EnumerateFiles(existingRoot, "*", SearchOption.AllDirectories))
        {
            var relToTarget = Path.GetRelativePath(existingRoot, file);
            expectedTargets.Add(Path.Combine(targetRelativeRoot, relToTarget));
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
        BackupTargetPathGuard.EnsureRootIsSafe(backupRoot);
        try
        {
            BackupSourcePathGuard.EnsureFileIsSafe(file.SourcePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return;
        }
        catch (Exception ex) when (ex is InvalidDataException
                                   or UnauthorizedAccessException
                                   or IOException)
        {
            stats.AddIssue(ex, $"{file.SourcePath}: Quelldatei nicht sicher lesbar ({ex.Message})");
            expectedTargets.Add(file.TargetRelativePath);
            return;
        }

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
        BackupTargetPathGuard.EnsureRootIsSafe(backupRoot);
        foreach (var file in EnumerateFiles(
                     backupRoot,
                     BackupVersionRetention.IsVersionsDir,
                     stats,
                     linksAreErrors: true))
        {
            var rel = Path.GetRelativePath(backupRoot, file);
            if (expectedTargets.Contains(rel))
                continue;
            if (!BackupTargetGuard.IsInsideBackupRoot(backupRoot, file))
                continue;
            // Die String-Pruefung erkennt keine Junctions: eine Verknuepfung in der
            // Pfadkette unterhalb des Roots wuerde das Loeschziel aus dem Spiegel
            // heraus auf fremde Dateien umlenken — solche Pfade nie anfassen.
            if (ReparsePointGuard.HasReparsePointBelow(backupRoot, file))
            {
                stats.Errors.Add($"{file}: Verknuepfung im Zielpfad - Entfernen uebersprungen");
                continue;
            }

            try
            {
                BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, file);
                if (_versionsStandName is null)
                    File.Delete(file);
                else
                    MoveToVersions(backupRoot, rel, file);
                stats.Deleted++;
            }
            catch (Exception ex)
            {
                // Eine stehengebliebene Altdatei im Spiegel ist unschoen, aber
                // ungefaehrlich — nur eine verletzte Zielgrenze bricht ab.
                stats.AddIssue(ex, $"{file}: Entfernen fehlgeschlagen ({ex.Message})");
            }
        }

        DeleteEmptyDirectories(backupRoot, stats);
    }

    /// <summary>Verschiebt eine Spiegel-Datei in den Stand-Ordner dieses Laufs.</summary>
    private void MoveToVersions(string backupRoot, string targetRel, string file)
    {
        var versionsRel = BackupVersionRetention.BuildVersionsRelativePath(_versionsStandName!, targetRel);
        var versionsPath = BackupTargetPathGuard.ResolveRelativePath(backupRoot, versionsRel);
        var versionsDirectory = Path.GetDirectoryName(versionsPath)!;
        BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, file);
        BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, versionsDirectory);
        Directory.CreateDirectory(versionsDirectory);
        BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, versionsPath);
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
        try
        {
            BackupSourcePathGuard.EnsureFileIsSafe(sourceFile);
            var targetFile = BackupTargetPathGuard.ResolveRelativePath(backupRoot, targetRel);
            var sourceInfo = new FileInfo(sourceFile);
            var targetInfo = new FileInfo(targetFile);

            if (_sqliteSnapshots.IsSqliteDatabase(sourceFile))
            {
                await CopySqliteSnapshotAsync(
                        sourceFile, sourceInfo, backupRoot, targetRel, targetFile,
                        stats, onFileDone, ct)
                    .ConfigureAwait(false);
                return;
            }

            var unchanged = await IsUnchangedAsync(sourceInfo, targetInfo, ct).ConfigureAwait(false);

            if (unchanged)
            {
                stats.Unchanged++;
                onFileDone?.Invoke(sourceFile, sourceInfo.Length);
                return;
            }

            var targetDirectory = Path.GetDirectoryName(targetFile)!;
            BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, targetDirectory);
            Directory.CreateDirectory(targetDirectory);
            BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, targetDirectory);

            // Erst in Temp-Datei schreiben, dann atomar umbenennen — halbfertige
            // Kopien (Absturz/Abbruch) werden so nie faelschlich als aktuell gewertet.
            var tempFile = targetFile + TempSuffix;
            try
            {
                BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, tempFile);
                var copiedInfo = await CopyNormalFileVerifiedAsync(
                        sourceFile, backupRoot, tempFile, ct)
                    .ConfigureAwait(false);
                BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, targetFile);
                TryMoveOldVersionAside(backupRoot, targetRel, targetFile, stats);
                BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, tempFile);
                BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, targetFile);
                File.Move(tempFile, targetFile, overwrite: true);

                stats.Verified++;
                stats.Copied++;
                stats.BytesCopied += copiedInfo.Length;
                onFileDone?.Invoke(sourceFile, copiedInfo.Length);
            }
            catch
            {
                TryDeleteTemp(backupRoot, tempFile);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or PathTooLongException
                                   or NotSupportedException
                                   or InvalidDataException
                                   or Microsoft.Data.Sqlite.SqliteException)
        {
            // Eine einzelne gesperrte oder nicht lesbare Quelldatei darf die
            // gesamte Sicherung nicht scheitern lassen: Ihr Ziel steht bereits
            // in expectedTargets, der bisherige Stand bleibt also erhalten.
            stats.AddIssue(ex, $"{sourceFile}: {ex.Message}");
        }
    }

    internal static async Task<bool> IsUnchangedAsync(
        FileInfo sourceInfo,
        FileInfo targetInfo,
        CancellationToken ct)
    {
        var sameLength = targetInfo.Exists && targetInfo.Length == sourceInfo.Length;
        var timestampDifference = targetInfo.Exists
            ? (targetInfo.LastWriteTimeUtc - sourceInfo.LastWriteTimeUtc).Duration()
            : TimeSpan.MaxValue;
        var unchanged = sameLength && timestampDifference == TimeSpan.Zero;

        // FAT/exFAT runden Zeitstempel auf bis zu zwei Sekunden. Bei einem kleinen,
        // aber echten Unterschied darf gleiche Dateigroesse allein nicht genuegen.
        if (!unchanged && sameLength && timestampDifference <= TimestampToleranz)
        {
            unchanged = await FilesHaveSameContentAsync(
                    sourceInfo.FullName,
                    targetInfo.FullName,
                    sourceInfo.Length,
                    sourceInfo.LastWriteTimeUtc,
                    ct)
                .ConfigureAwait(false);
        }

        return unchanged;
    }

    private async Task CopySqliteSnapshotAsync(
        string sourceFile,
        FileInfo sourceInfo,
        string backupRoot,
        string targetRel,
        string targetFile,
        MirrorStats stats,
        Action<string, long>? onFileDone,
        CancellationToken ct)
    {
        var targetDirectory = Path.GetDirectoryName(targetFile)!;
        BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, targetDirectory);
        Directory.CreateDirectory(targetDirectory);
        BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, targetDirectory);
        var tempFile = targetFile + TempSuffix;
        try
        {
            BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, tempFile);
            await _sqliteSnapshots.CreateVerifiedSnapshotAsync(
                    sourceFile, tempFile, _afterTemporaryFileWritten, ct)
                .ConfigureAwait(false);

            BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, tempFile);
            var effectiveWriteTime = GetEffectiveSqliteWriteTimeUtc(sourceFile, sourceInfo.LastWriteTimeUtc);
            File.SetLastWriteTimeUtc(tempFile, effectiveWriteTime);
            var tempInfo = new FileInfo(tempFile);
            // Laenge vor dem atomaren Verschieben merken. FileInfo zeigt danach noch
            // auf den alten Temp-Pfad und wuerde sonst faelschlich FileNotFound werfen,
            // obwohl die Datenbank bereits korrekt im Ziel liegt.
            var tempLength = tempInfo.Length;
            stats.DatabasesSnapshotted++;
            stats.Verified++;

            if (File.Exists(targetFile)
                && await FilesHaveSameContentAsync(
                        tempFile, targetFile, tempInfo.Length, tempInfo.LastWriteTimeUtc, ct)
                    .ConfigureAwait(false))
            {
                TryDeleteTemp(backupRoot, tempFile);
                stats.Unchanged++;
                onFileDone?.Invoke(sourceFile, sourceInfo.Length);
                return;
            }

            BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, targetFile);
            TryMoveOldVersionAside(backupRoot, targetRel, targetFile, stats);
            BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, tempFile);
            BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, targetFile);
            File.Move(tempFile, targetFile, overwrite: true);
            stats.Copied++;
            stats.BytesCopied += tempLength;
            onFileDone?.Invoke(sourceFile, sourceInfo.Length);
        }
        catch
        {
            TryDeleteTemp(backupRoot, tempFile);
            throw;
        }
    }

    private async Task<VerifiedCopyInfo> CopyNormalFileVerifiedAsync(
        string sourceFile,
        string backupRoot,
        string tempFile,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxStableCopyAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var before = new FileInfo(sourceFile);
            byte[] sourceHash;

            BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, tempFile);
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (var src = new FileStream(
                       sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                       bufferSize: 128 * 1024, useAsync: true))
            using (var dst = new FileStream(
                       tempFile, FileMode.Create, FileAccess.Write, FileShare.None,
                       bufferSize: 128 * 1024, useAsync: true))
            {
                var buffer = new byte[128 * 1024];
                int read;
                while ((read = await src.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                }

                await dst.FlushAsync(ct).ConfigureAwait(false);
                dst.Flush(flushToDisk: true);
                sourceHash = hash.GetHashAndReset();
            }

            _afterTemporaryFileWritten?.Invoke(tempFile);
            BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, tempFile);
            var tempHash = await HashFileAsync(tempFile, FileShare.Read, ct).ConfigureAwait(false);
            if (!sourceHash.AsSpan().SequenceEqual(tempHash))
                throw new IOException("Vollstaendige Inhaltspruefung nach dem Kopieren fehlgeschlagen.");

            var after = new FileInfo(sourceFile);
            if (before.Length == after.Length && before.LastWriteTimeUtc == after.LastWriteTimeUtc)
            {
                BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, tempFile);
                File.SetLastWriteTimeUtc(tempFile, after.LastWriteTimeUtc);
                return new VerifiedCopyInfo(after.Length);
            }

            TryDeleteTemp(backupRoot, tempFile);
            if (attempt == MaxStableCopyAttempts)
            {
                throw new IOException(
                    $"Datei wurde waehrend des Kopierens mehrfach geaendert ({MaxStableCopyAttempts} Versuche).");
            }
        }

        throw new IOException("Datei konnte nicht stabil kopiert werden.");
    }

    private static DateTime GetEffectiveSqliteWriteTimeUtc(string databasePath, DateTime databaseWriteTimeUtc)
    {
        var walPath = databasePath + "-wal";
        if (!File.Exists(walPath))
            return databaseWriteTimeUtc;

        var walWriteTime = File.GetLastWriteTimeUtc(walPath);
        return walWriteTime > databaseWriteTimeUtc ? walWriteTime : databaseWriteTimeUtc;
    }

    private static async Task<byte[]> HashFileAsync(string path, FileShare share, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, share,
            bufferSize: 128 * 1024, useAsync: true);
        return await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
    }

    private sealed record VerifiedCopyInfo(long Length);

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
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or PathTooLongException
                                   or NotSupportedException
                                   or InvalidDataException)
        {
            stats.AddIssue(
                ex,
                $"{targetFile}: Vorversion nicht nach {BackupVersionRetention.VersionsFolderName} verschoben ({ex.Message})");
        }
    }

    /// <summary>
    /// Rekursive Datei-Enumeration, die ausgeschlossene Ordner gar nicht erst betritt
    /// (Muster SafeFileEnumeration: Stack-basiert, Fehler pro Ordner abgefangen).
    /// Verknuepfungen/Junctions (Dateien wie Ordner) werden uebersprungen und
    /// gemeldet — dahinter liegt Inhalt ausserhalb des eigenen Baums.
    /// Das Praedikat bekommt den Ordnerpfad relativ zum Root.
    /// </summary>
    /// <param name="linksAreErrors">
    /// true beim Durchlauf des ZIELBAUMS: dort ist eine Verknuepfung eine verletzte
    /// Sicherheitsgrenze und muss den Lauf stoppen. false bei einer QUELLE: dort ist
    /// sie nur ein uebersprungener Fremdinhalt (etwa der Ordner "artifacts" mit
    /// seinen Verknuepfungen auf die Sidecar-Modelle) und darf die Sicherung nicht
    /// abbrechen.
    /// </param>
    /// <param name="onSkippedFile">
    /// Wird fuer jede uebersprungene Datei gerufen, damit der Aufrufer ihren
    /// bisherigen Spiegelstand vor der Verwaisten-Loeschung schuetzen kann.
    /// </param>
    private static IEnumerable<string> EnumerateFiles(
        string root,
        Func<string, bool>? isDirExcluded,
        MirrorStats stats,
        bool linksAreErrors,
        Action<string>? onSkippedFile = null)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            string[] files;
            string[] children;
            try
            {
                BackupSourcePathGuard.EnsureDirectoryRootIsSafe(current);
                files = Directory.EnumerateFiles(current)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                children = Directory.EnumerateDirectories(current)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or InvalidDataException)
            {
                stats.Errors.Add($"{current}: Ordner nicht lesbar ({ex.Message})");
                continue;
            }

            foreach (var file in files)
            {
                // Datei-Verknuepfung nicht folgen: das Kopieren wuerde sonst still
                // Inhalt ausserhalb der Quelle lesen.
                try
                {
                    BackupSourcePathGuard.EnsureFileIsSafe(file);
                }
                catch (Exception ex) when (ex is IOException
                                           or UnauthorizedAccessException
                                           or InvalidDataException)
                {
                    Report(stats, linksAreErrors, $"{file}: Quelldatei nicht sicher lesbar ({ex.Message})");
                    onSkippedFile?.Invoke(file);
                    continue;
                }

                yield return file;
            }

            for (var i = children.Length - 1; i >= 0; i--)
            {
                // Junction/Symlink nicht betreten: dahinter liegt fremder Inhalt,
                // der weder gespiegelt noch als verwaist geloescht werden darf.
                if (ReparsePointGuard.IsReparsePoint(children[i]))
                {
                    Report(stats, linksAreErrors, $"{children[i]}: Verknuepfung/Junction uebersprungen");
                    continue;
                }

                var relDir = Path.GetRelativePath(root, children[i]);
                if (isDirExcluded is null || !isDirExcluded(relDir))
                    stack.Push(children[i]);
            }
        }
    }

    private static void Report(MirrorStats stats, bool asError, string message)
    {
        if (asError)
            stats.Errors.Add(message);
        else
            stats.Warnings.Add(message);
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
            // Leere Ordner sind nur Kosmetik — kein Grund, die Sicherung zu verwerfen.
            stats.Warnings.Add($"{backupRoot}: Ordner-Aufraeumen fehlgeschlagen ({ex.Message})");
            return;
        }

        foreach (var dir in dirs)
        {
            if (!BackupTargetGuard.IsInsideBackupRoot(backupRoot, dir))
                continue;

            // Die AllDirectories-Enumeration folgt Junctions: ueber eine solche Kette
            // duerfen keine fremden (leeren) Ordner geloescht werden. Die Junction
            // selbst wurde beim Datei-Durchlauf bereits gemeldet, daher hier still.
            if (ReparsePointGuard.HasReparsePointBelow(backupRoot, dir))
                continue;

            try
            {
                BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, dir);
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, dir);
                    Directory.Delete(dir);
                }
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

    private static void TryDeleteTemp(string backupRoot, string tempFile)
    {
        try
        {
            if (File.Exists(tempFile))
            {
                BackupTargetPathGuard.EnsurePathIsSafe(backupRoot, tempFile);
                File.Delete(tempFile);
            }
        }
        catch
        {
            // Best effort — Reste raeumt die Verwaisten-Loeschung des Folgelaufs ab.
        }
    }
}
