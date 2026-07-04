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
    private readonly DirectoryMirror _mirror = new();

    public FullBackupService(
        Func<FullBackupSources> quellenFactory,
        Action? walCheckpoint = null,
        Func<CancellationToken, Task<string?>>? ollamaListe = null)
    {
        _sourcesFactory = quellenFactory ?? throw new ArgumentNullException(nameof(quellenFactory));
        _walCheckpoint = walCheckpoint;
        _ollamaList = ollamaListe;
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

            var markerError = BackupTargetGuard.ValidateAndCreateMarker(backupRoot);
            if (markerError is not null)
                return Failure(markerError, backupRoot, started.Elapsed);

            _walCheckpoint?.Invoke();

            var sizeReport = Analyze(sources, progress: null, ct);
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
                    await _mirror.MirrorSourceAsync(
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
                        await _mirror.MirrorFileAsync(
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
            _mirror.DeleteOrphans(backupRoot, expectedTargets, stats);

            var skipped = stats.Errors.Take(200).ToArray();
            var manifest = BuildManifest(sources, plan, sizeReport, stats, skipped);
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
                Duration: started.Elapsed);
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

    private static IEnumerable<string> EnumerateFiles(string root, Func<string, bool>? isDirExcluded)
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
                yield return file;

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
                sb.AppendLine($"  {kv.Key}={kv.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("Ollama Modelle:");
        var ollama = _ollamaList is null ? null : await _ollamaList(ct).ConfigureAwait(false);
        sb.AppendLine(string.IsNullOrWhiteSpace(ollama) ? "  (ollama list nicht verfuegbar)" : ollama.TrimEnd());
        return sb.ToString();
    }

    private static object BuildManifest(
        FullBackupSources sources,
        IReadOnlyList<BackupComponent> plan,
        FullBackupSizeReport sizeReport,
        DirectoryMirror.MirrorStats stats,
        IReadOnlyList<string> skipped)
        => new
        {
            CreatedUtc = DateTimeOffset.UtcNow,
            sources.AppVersion,
            GitCommit = GitCommitResolver.Resolve(sources.RepoRoot),
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
                stats.Deleted
            },
            Plan = plan.Select(c => new
            {
                c.Name,
                Sources = c.Sources.Select(s => new { s.SourceRoot, s.TargetRelativeRoot }).ToArray(),
                Files = c.Files?.Select(f => new { f.SourcePath, f.TargetRelativePath }).ToArray()
            }).ToArray(),
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

    private static FullBackupResult Failure(string error, string backupRoot, TimeSpan duration)
        => new(
            Success: false,
            Error: error,
            TargetRoot: backupRoot,
            TotalBytes: 0,
            FilesCopied: 0,
            FilesUnchanged: 0,
            FilesDeleted: 0,
            SkippedFiles: Array.Empty<string>(),
            Duration: duration);

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
