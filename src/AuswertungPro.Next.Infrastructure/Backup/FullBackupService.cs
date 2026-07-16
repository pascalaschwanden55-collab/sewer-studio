using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Orchestriert die komplette Datensicherung als inkrementellen Spiegel.
/// Loeschungen passieren ausschliesslich unterhalb des markierten Backup-Roots.
/// </summary>
public sealed class FullBackupService : IFullBackupService
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly Func<FullBackupSources> _sourcesFactory;
    private readonly Action? _walCheckpoint;
    private readonly Func<CancellationToken, Task<string?>>? _ollamaList;
    private readonly Func<string, long?> _availableBytes;
    private readonly IGitCommitResolver _gitCommitResolver;
    private readonly IBackupTargetMarkerGuard _targetMarkerGuard;
    private readonly ISqliteSnapshotCopier _sqliteSnapshots;
    private readonly IBackupManifestIntegrityService _manifestIntegrity;

    public FullBackupService(
        Func<FullBackupSources> quellenFactory,
        Action? walCheckpoint = null,
        Func<CancellationToken, Task<string?>>? ollamaListe = null,
        Func<string, long?>? availableBytes = null,
        IGitCommitResolver? gitCommitResolver = null)
        : this(
            quellenFactory,
            walCheckpoint,
            ollamaListe,
            availableBytes,
            gitCommitResolver,
            BackupTargetGuard.MarkerGuard,
            new SqliteSnapshotCopyService())
    {
    }

    public FullBackupService(
        Func<FullBackupSources> quellenFactory,
        IBackupTargetMarkerGuard targetMarkerGuard)
        : this(
            quellenFactory,
            walCheckpoint: null,
            ollamaListe: null,
            availableBytes: null,
            gitCommitResolver: null,
            targetMarkerGuard: targetMarkerGuard,
            sqliteSnapshots: new SqliteSnapshotCopyService())
    {
    }

    public FullBackupService(
        Func<FullBackupSources> quellenFactory,
        Action? walCheckpoint,
        Func<CancellationToken, Task<string?>>? ollamaListe,
        Func<string, long?>? availableBytes,
        IGitCommitResolver? gitCommitResolver,
        IBackupTargetMarkerGuard targetMarkerGuard)
        : this(
            quellenFactory,
            walCheckpoint,
            ollamaListe,
            availableBytes,
            gitCommitResolver,
            targetMarkerGuard,
            new SqliteSnapshotCopyService())
    {
    }

    public FullBackupService(
        Func<FullBackupSources> quellenFactory,
        Action? walCheckpoint,
        Func<CancellationToken, Task<string?>>? ollamaListe,
        Func<string, long?>? availableBytes,
        IGitCommitResolver? gitCommitResolver,
        IBackupTargetMarkerGuard targetMarkerGuard,
        ISqliteSnapshotCopier sqliteSnapshots,
        IBackupManifestIntegrityService? manifestIntegrity = null)
    {
        _sourcesFactory = quellenFactory ?? throw new ArgumentNullException(nameof(quellenFactory));
        _walCheckpoint = walCheckpoint;
        _ollamaList = ollamaListe;
        _availableBytes = availableBytes ?? BackupDiskSpaceGuard.GetAvailableBytes;
        _gitCommitResolver = gitCommitResolver ?? GitCommitResolver.DefaultResolver;
        _targetMarkerGuard = targetMarkerGuard ?? throw new ArgumentNullException(nameof(targetMarkerGuard));
        _sqliteSnapshots = sqliteSnapshots ?? throw new ArgumentNullException(nameof(sqliteSnapshots));
        _manifestIntegrity = manifestIntegrity ?? BackupManifestIntegrity.Current;
    }

    public Task<FullBackupSizeReport> AnalyzeAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var sources = _sourcesFactory();
        return Task.FromResult(Analyze(sources, progress, ct));
    }

    public async Task<FullBackupResult> RunAsync(
        string targetFolder,
        IProgress<FullBackupProgress>? progress = null,
        CancellationToken ct = default)
    {
        var started = Stopwatch.StartNew();
        var backupRoot = Path.Combine(targetFolder, BackupPlanBuilder.TargetFolderName);

        try
        {
            var sources = _sourcesFactory();
            var plan = BackupPlanBuilder.Build(sources);
            var conflict = BackupTargetGuard.CheckSourceTargetConflict(backupRoot, CollectSourceRoots(plan));
            if (conflict is not null)
                return Failure(conflict, backupRoot, started.Elapsed);

            var markerError = _targetMarkerGuard.ValidateAndCreateMarker(backupRoot);
            if (markerError is not null)
                return Failure(markerError, backupRoot, started.Elapsed);

            _walCheckpoint?.Invoke();

            // Pro Lauf ein datierter Versions-Stand: ersetzte/entfallene Dateien
            // wandern dorthin statt endgueltig zu verschwinden.
            var mirror = new DirectoryMirror(
                BackupVersionRetention.BuildStandName(DateTime.Now),
                afterTemporaryFileWritten: null,
                sqliteSnapshots: _sqliteSnapshots);

            var sizeReport = Analyze(sources, progress: null, ct);
            var bytesToWrite = await EstimateRequiredCopyBytesAsync(plan, backupRoot, ct)
                .ConfigureAwait(false);
            var requiredFreeBytes = checked(bytesToWrite + BackupDiskSpaceGuard.MinimumReserveBytes);
            var availableFreeBytes = _availableBytes(backupRoot);
            var spaceError = BackupDiskSpaceGuard.Validate(requiredFreeBytes, availableFreeBytes);
            if (spaceError is not null)
            {
                return Failure(
                    spaceError, backupRoot, started.Elapsed,
                    requiredFreeBytes, availableFreeBytes ?? 0);
            }
            var confirmedAvailableBytes = availableFreeBytes.GetValueOrDefault();

            var stats = new DirectoryMirror.MirrorStats();
            var expectedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                BackupPlanBuilder.MarkerFileName,
                "manifest.json",
                "manifest.json.bak",
                Path.Combine("Extras", "umgebung.txt"),
                Path.Combine("Extras", "RESTORE-ANLEITUNG.txt")
            };

            var progressState = new ProgressState(sizeReport.TotalBytes, sizeReport.TotalFiles);
            foreach (var component in plan)
            {
                foreach (var source in component.Sources)
                {
                    await mirror.MirrorSourceAsync(
                        source,
                        backupRoot,
                        expectedTargets,
                        stats,
                        (file, bytes) => progressState.FileDone(progress, component.Name, file, bytes),
                        ct).ConfigureAwait(false);
                }

                if (component.Files is not null)
                {
                    foreach (var file in component.Files)
                    {
                        await mirror.MirrorFileAsync(
                            file,
                            backupRoot,
                            expectedTargets,
                            stats,
                            (currentFile, bytes) => progressState.FileDone(progress, component.Name, currentFile, bytes),
                            ct).ConfigureAwait(false);
                    }
                }
            }

            progressState.Report(progress, "Extras", "umgebung.txt", force: true);
            await WriteGeneratedExtrasAsync(backupRoot, sources, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();
            mirror.RemoveOrphans(backupRoot, expectedTargets, stats);
            var versionStaende = RotateVersionStaende(backupRoot, stats);

            var skipped = stats.Errors.Take(200).ToArray();
            var hashProgressThrottle = Stopwatch.StartNew();
            var manifestFiles = await _manifestIntegrity.CreateEntriesAsync(
                    backupRoot,
                    file =>
                    {
                        if (progress is null || hashProgressThrottle.ElapsedMilliseconds < 250)
                            return;

                        hashProgressThrottle.Restart();
                        progress.Report(new FullBackupProgress(
                            "Pruefe Sicherung",
                            Path.GetRelativePath(backupRoot, file),
                            sizeReport.TotalBytes,
                            sizeReport.TotalBytes,
                            sizeReport.TotalFiles,
                            sizeReport.TotalFiles));
                    },
                    ct)
                .ConfigureAwait(false);
            progressState.Report(progress, "Pruefe Sicherung", "SHA-256 abgeschlossen", force: true);
            var manifest = BuildManifest(
                sources, plan, sizeReport, stats, skipped, versionStaende,
                requiredFreeBytes, confirmedAvailableBytes, manifestFiles);
            var manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions);
            await AtomicTextFileWriter.WriteAllTextAsync(
                Path.Combine(backupRoot, "manifest.json"),
                manifestJson,
                ct).ConfigureAwait(false);

            progressState.Report(progress, "Fertig", "manifest.json", force: true);

            return new FullBackupResult(
                Success: true,
                Error: null,
                TargetRoot: backupRoot,
                TotalBytes: sizeReport.TotalBytes,
                FilesCopied: stats.Copied,
                FilesUnchanged: stats.Unchanged,
                FilesDeleted: stats.Deleted,
                SkippedFiles: skipped,
                Duration: started.Elapsed,
                FilesVerified: stats.Verified,
                DatabasesSnapshotted: stats.DatabasesSnapshotted,
                RequiredFreeBytes: requiredFreeBytes,
                AvailableFreeBytes: confirmedAvailableBytes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Failure(ex.Message, backupRoot, started.Elapsed);
        }
    }

    private FullBackupSizeReport Analyze(
        FullBackupSources sources,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var components = new List<ComponentSize>();
        var plan = BackupPlanBuilder.Build(sources);

        foreach (var component in plan)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(component.Name);

            long bytes = 0;
            var files = 0;
            var sourceFound = false;

            foreach (var source in component.Sources)
            {
                if (!Directory.Exists(source.SourceRoot))
                    continue;

                sourceFound = true;
                foreach (var file in EnumerateFiles(source.SourceRoot, source.IsDirExcluded))
                {
                    ct.ThrowIfCancellationRequested();
                    var relative = Path.GetRelativePath(source.SourceRoot, file);
                    if (source.IsFileExcluded?.Invoke(relative) == true)
                        continue;
                    TryAddFileSize(file, ref bytes, ref files);
                }
            }

            if (component.Files is not null)
            {
                foreach (var file in component.Files)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!File.Exists(file.SourcePath))
                        continue;

                    sourceFound = true;
                    TryAddFileSize(file.SourcePath, ref bytes, ref files);
                }
            }

            components.Add(new ComponentSize(
                component.Name,
                component.Beschreibung,
                bytes,
                files,
                sourceFound));
        }

        return new FullBackupSizeReport(
            components,
            components.Sum(c => c.Bytes),
            components.Sum(c => c.FileCount));
    }

    private static void TryAddFileSize(string file, ref long bytes, ref int files)
    {
        try
        {
            var info = new FileInfo(file);
            bytes += info.Length;
            files++;
        }
        catch
        {
            // Analyse ist best effort. Der eigentliche Kopierlauf protokolliert Datei-Fehler.
        }
    }

    private IEnumerable<string> EnumerateFiles(string root, Func<string, bool>? isDirExcluded)
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (!_sqliteSnapshots.IsCompanionOfSqliteDatabase(file))
                    yield return file;
            }

            for (var i = children.Length - 1; i >= 0; i--)
            {
                var relative = Path.GetRelativePath(root, children[i]);
                if (isDirExcluded is null || !isDirExcluded(relative))
                    stack.Push(children[i]);
            }
        }
    }

    private async Task WriteGeneratedExtrasAsync(string backupRoot, FullBackupSources sources, CancellationToken ct)
    {
        var extrasDir = Path.Combine(backupRoot, "Extras");
        Directory.CreateDirectory(extrasDir);

        await File.WriteAllTextAsync(
            Path.Combine(extrasDir, "RESTORE-ANLEITUNG.txt"),
            RestoreAnleitungText.Build(sources),
            Encoding.UTF8,
            ct).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(extrasDir, "umgebung.txt"),
            await BuildUmgebungTextAsync(sources, ct).ConfigureAwait(false),
            Encoding.UTF8,
            ct).ConfigureAwait(false);
    }

    private async Task<long> EstimateRequiredCopyBytesAsync(
        IReadOnlyList<BackupComponent> plan,
        string backupRoot,
        CancellationToken ct)
    {
        long required = 0;

        foreach (var component in plan)
        {
            foreach (var source in component.Sources)
            {
                foreach (var sourceFile in EnumerateFiles(source.SourceRoot, source.IsDirExcluded))
                {
                    ct.ThrowIfCancellationRequested();
                    var relative = Path.GetRelativePath(source.SourceRoot, sourceFile);
                    if (source.IsFileExcluded?.Invoke(relative) == true)
                        continue;
                    var targetFile = Path.Combine(backupRoot, source.TargetRelativeRoot, relative);
                    required = checked(required + await EstimateFileBytesAsync(sourceFile, targetFile, ct)
                        .ConfigureAwait(false));
                }
            }

            if (component.Files is null)
                continue;

            foreach (var file in component.Files)
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(file.SourcePath))
                    continue;

                var targetFile = Path.Combine(backupRoot, file.TargetRelativePath);
                required = checked(required + await EstimateFileBytesAsync(file.SourcePath, targetFile, ct)
                    .ConfigureAwait(false));
            }
        }

        return required;
    }

    private async Task<long> EstimateFileBytesAsync(
        string sourceFile,
        string targetFile,
        CancellationToken ct)
    {
        if (_sqliteSnapshots.IsSqliteDatabase(sourceFile))
            return _sqliteSnapshots.GetConservativeSnapshotBytes(sourceFile);

        var sourceInfo = new FileInfo(sourceFile);
        var targetInfo = new FileInfo(targetFile);
        return await DirectoryMirror.IsUnchangedAsync(sourceInfo, targetInfo, ct).ConfigureAwait(false)
            ? 0
            : sourceInfo.Length;
    }

    private async Task<string> BuildUmgebungTextAsync(FullBackupSources sources, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SewerStudio Umgebung");
        sb.AppendLine("====================");
        sb.AppendLine($"Erstellt: {DateTimeOffset.Now:O}");
        sb.AppendLine($"App-Version: {sources.AppVersion}");
        sb.AppendLine();
        sb.AppendLine("Quellpfade:");
        sb.AppendLine($"  RepoRoot: {sources.RepoRoot ?? "(nicht gefunden)"}");
        sb.AppendLine($"  KI_BRAIN: {sources.KnowledgeRoot}");
        sb.AppendLine($"  Local SewerStudio: {sources.LocalSewerStudioDir}");
        sb.AppendLine($"  Roaming SewerStudio: {sources.RoamingSewerStudioDir}");
        sb.AppendLine($"  Roaming AuswertungPro: {sources.RoamingAuswertungProDir}");
        sb.AppendLine($"  Desktop: {sources.DesktopDir}");
        sb.AppendLine();
        sb.AppendLine("Umgebungsvariablen:");
        if (sources.EnvironmentVariables.Count == 0)
        {
            sb.AppendLine("  (keine SEWERSTUDIO_*/SEWER_*-Variablen gefunden)");
        }
        else
        {
            foreach (var kv in sources.EnvironmentVariables.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"  {kv.Key}={BackupEnvironmentVariableRedactor.RedactValue(kv.Key, kv.Value)}");
        }

        sb.AppendLine();
        sb.AppendLine("Ollama Modelle:");
        var ollama = _ollamaList is null ? null : await _ollamaList(ct).ConfigureAwait(false);
        sb.AppendLine(string.IsNullOrWhiteSpace(ollama) ? "  (ollama list nicht verfuegbar)" : ollama.TrimEnd());
        return sb.ToString();
    }

    /// <summary>
    /// Entfernt die aeltesten Versions-Staende ueber dem Aufbewahrungslimit.
    /// Liefert die Anzahl der verbleibenden Staende (fuer das Manifest).
    /// </summary>
    private static int RotateVersionStaende(string backupRoot, DirectoryMirror.MirrorStats stats)
    {
        var versionsRoot = Path.Combine(backupRoot, BackupVersionRetention.VersionsFolderName);
        if (!Directory.Exists(versionsRoot))
            return 0;

        var namen = Directory.EnumerateDirectories(versionsRoot)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToArray();

        foreach (var name in BackupVersionRetention.SelectStaendeToDelete(namen))
        {
            var standDir = Path.Combine(versionsRoot, name);
            if (!BackupTargetGuard.IsInsideBackupRoot(backupRoot, standDir))
                continue;

            try
            {
                Directory.Delete(standDir, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                stats.Errors.Add($"{standDir}: Alter Versions-Stand nicht entfernt ({ex.Message})");
            }
        }

        return Directory.Exists(versionsRoot)
            ? Directory.EnumerateDirectories(versionsRoot).Count()
            : 0;
    }

    private object BuildManifest(
        FullBackupSources sources,
        IReadOnlyList<BackupComponent> plan,
        FullBackupSizeReport sizeReport,
        DirectoryMirror.MirrorStats stats,
        IReadOnlyList<string> skipped,
        int versionStaende,
        long requiredFreeBytes,
        long availableFreeBytes,
        IReadOnlyList<BackupManifestFileEntry> files)
        => new
        {
            CreatedUtc = DateTimeOffset.UtcNow,
            sources.AppVersion,
            GitCommit = _gitCommitResolver.Resolve(sources.RepoRoot),
            SourcePaths = new
            {
                sources.RepoRoot,
                sources.KnowledgeRoot,
                sources.LocalSewerStudioDir,
                sources.RoamingSewerStudioDir,
                sources.RoamingAuswertungProDir,
                sources.DesktopDir
            },
            Components = sizeReport.Components.Select(c => new
            {
                c.Name,
                c.Beschreibung,
                c.Bytes,
                c.FileCount,
                c.SourceFound
            }).ToArray(),
            Totals = new
            {
                sizeReport.TotalBytes,
                sizeReport.TotalFiles,
                stats.Copied,
                stats.Unchanged,
                stats.Deleted,
                stats.Verified,
                stats.DatabasesSnapshotted
            },
            Versionen = new
            {
                Staende = versionStaende,
                BackupVersionRetention.MaxStaende
            },
            Speicherplatz = new
            {
                RequiredFreeBytes = requiredFreeBytes,
                AvailableFreeBytes = availableFreeBytes,
                ReserveBytes = BackupDiskSpaceGuard.MinimumReserveBytes
            },
            Plan = plan.Select(c => new
            {
                c.Name,
                Sources = c.Sources.Select(s => new { s.SourceRoot, s.TargetRelativeRoot }).ToArray(),
                Files = c.Files?.Select(f => new { f.SourcePath, f.TargetRelativePath }).ToArray()
            }).ToArray(),
            Files = files,
            SkippedFiles = skipped
        };

    private static IEnumerable<string> CollectSourceRoots(IReadOnlyList<BackupComponent> plan)
    {
        foreach (var component in plan)
        {
            foreach (var source in component.Sources)
                yield return source.SourceRoot;

            if (component.Files is null)
                continue;

            foreach (var file in component.Files)
            {
                var directory = Path.GetDirectoryName(file.SourcePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    yield return directory;
            }
        }
    }

    private static FullBackupResult Failure(
        string error,
        string backupRoot,
        TimeSpan duration,
        long requiredFreeBytes = 0,
        long availableFreeBytes = 0)
        => new(
            Success: false,
            Error: error,
            TargetRoot: backupRoot,
            TotalBytes: 0,
            FilesCopied: 0,
            FilesUnchanged: 0,
            FilesDeleted: 0,
            SkippedFiles: Array.Empty<string>(),
            Duration: duration,
            FilesVerified: 0,
            DatabasesSnapshotted: 0,
            RequiredFreeBytes: requiredFreeBytes,
            AvailableFreeBytes: availableFreeBytes);

    private sealed class ProgressState(long bytesTotal, int filesTotal)
    {
        private readonly Stopwatch _throttle = Stopwatch.StartNew();
        private long _bytesDone;
        private int _filesDone;

        public void FileDone(
            IProgress<FullBackupProgress>? progress,
            string component,
            string currentFile,
            long bytes)
        {
            _bytesDone += bytes;
            _filesDone++;
            Report(progress, component, currentFile, force: false);
        }

        public void Report(
            IProgress<FullBackupProgress>? progress,
            string component,
            string currentFile,
            bool force)
        {
            if (progress is null)
                return;

            if (!force && _throttle.ElapsedMilliseconds < 250 && _filesDone < filesTotal)
                return;

            _throttle.Restart();
            progress.Report(new FullBackupProgress(
                component,
                currentFile,
                _bytesDone,
                bytesTotal,
                _filesDone,
                filesTotal));
        }
    }
}
