using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Backup;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Gleicht den vollständigen KnowledgeRoot beim Start ab und übernimmt danach
/// Dateiänderungen laufend auf den externen Spiegel.
/// </summary>
public sealed class KnowledgeRealtimeMirrorService : IKnowledgeRealtimeMirrorService
{
    public const string MarkerFileName = ".sewerstudio-ki-brain-mirror";

    private static readonly TimeSpan DefaultScanInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);
    private static readonly string[] PreservedTargetFiles =
    {
        MarkerFileName,
        "manifest.json",
        "_spiegel_log.txt"
    };

    private readonly string _sourceRoot;
    private readonly Func<string?> _targetResolver;
    private readonly TimeSpan _scanInterval;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, byte> _pendingPaths =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _lifecycleGate = new();
    private readonly CancellationTokenSource _lifetime = new();

    private FileSystemWatcher? _watcher;
    private Task? _worker;
    private volatile string? _targetRoot;
    private volatile bool _needsFullScan = true;
    private DateTime _nextFullScanUtc = DateTime.MinValue;
    private bool _targetMissingWasLogged;
    private bool _disposed;

    public KnowledgeRealtimeMirrorService(string sourceRoot, ILogger<KnowledgeRealtimeMirrorService> logger)
        : this(
            sourceRoot,
            new KnowledgeMirrorTargetResolver().Resolve,
            DefaultScanInterval,
            logger)
    {
    }

    internal KnowledgeRealtimeMirrorService(
        string sourceRoot,
        Func<string?> targetResolver,
        TimeSpan scanInterval,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot))
            throw new ArgumentException("Quellordner fehlt.", nameof(sourceRoot));
        if (scanInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(scanInterval));

        _sourceRoot = NormalizeRoot(sourceRoot);
        _targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        _scanInterval = scanInterval;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string SourceRoot => _sourceRoot;

    public string? TargetRoot => _targetRoot;

    public bool IsRunning => _worker is { IsCompleted: false };

    public void Start()
    {
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_worker is { IsCompleted: false })
                return;

            EnsureWatcher();
            _worker = Task.Run(() => RunAsync(_lifetime.Token));
        }
    }

    public async Task SynchronizeNowAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var targetRoot = ResolveTargetOrThrow();
        await SynchronizeAllAsync(targetRoot, ct).ConfigureAwait(false);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_scanInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                string? targetRoot;
                try
                {
                    targetRoot = _targetResolver();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    LogTargetUnavailable(ex.Message);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(targetRoot))
                {
                    LogTargetUnavailable(
                        $"Datenträger \"{KnowledgeMirrorTargetResolver.DefaultVolumeLabel}\" ist nicht angeschlossen.");
                    continue;
                }

                targetRoot = NormalizeRoot(targetRoot);
                _targetMissingWasLogged = false;
                if (!string.Equals(_targetRoot, targetRoot, StringComparison.OrdinalIgnoreCase))
                {
                    _targetRoot = targetRoot;
                    _needsFullScan = true;
                }

                if (_needsFullScan && DateTime.UtcNow >= _nextFullScanUtc)
                {
                    // Vor dem Lauf zurücksetzen. Fordert der Watcher während des
                    // Abgleichs einen neuen Vollscan an, bleibt dieses neue Signal erhalten.
                    _needsFullScan = false;
                    try
                    {
                        await SynchronizeAllAsync(targetRoot, ct).ConfigureAwait(false);
                        _nextFullScanUtc = DateTime.MinValue;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _needsFullScan = true;
                        _nextFullScanUtc = DateTime.UtcNow + RetryDelay;
                        _logger.LogWarning(
                            ex,
                            "KI-Spiegel: Vollabgleich fehlgeschlagen. Nächster Versuch in {Seconds} Sekunden.",
                            RetryDelay.TotalSeconds);
                    }

                    continue;
                }

                if (!_needsFullScan)
                    await ProcessPendingPathsAsync(targetRoot, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normaler Programmabschluss.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KI-Spiegel: Hintergrundüberwachung wurde unerwartet beendet.");
        }
    }

    private void EnsureWatcher()
    {
        if (_watcher is not null)
            return;
        if (!Directory.Exists(_sourceRoot))
            throw new DirectoryNotFoundException($"KI-Quellordner nicht gefunden: {_sourceRoot}");

        var watcher = new FileSystemWatcher(_sourceRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                           | NotifyFilters.DirectoryName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.Size
                           | NotifyFilters.CreationTime,
            InternalBufferSize = 64 * 1024
        };

        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnWatcherError;
        watcher.EnableRaisingEvents = true;
        _watcher = watcher;
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
        => QueuePath(e.FullPath);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        QueuePath(e.OldFullPath);
        QueuePath(e.FullPath);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _needsFullScan = true;
        _nextFullScanUtc = DateTime.MinValue;
        _logger.LogWarning(
            e.GetException(),
            "KI-Spiegel: Dateiüberwachung meldet einen Überlauf. Ein Vollabgleich wird gestartet.");
    }

    private void QueuePath(string fullPath)
    {
        try
        {
            if (!TryGetRelativeSourcePath(fullPath, out var relativePath))
                return;

            if (Directory.Exists(fullPath))
            {
                _needsFullScan = true;
                _nextFullScanUtc = DateTime.MinValue;
                return;
            }

            _pendingPaths[relativePath] = 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _needsFullScan = true;
            _nextFullScanUtc = DateTime.MinValue;
            _logger.LogWarning(ex, "KI-Spiegel: Änderung konnte nicht eingeordnet werden: {Path}", fullPath);
        }
    }

    private async Task ProcessPendingPathsAsync(string targetRoot, CancellationToken ct)
    {
        var effectivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in _pendingPaths.Keys.ToArray())
        {
            ct.ThrowIfCancellationRequested();
            if (!_pendingPaths.TryRemove(relativePath, out _))
                continue;

            try
            {
                var effectivePath = GetEffectiveRelativePath(relativePath);
                if (!effectivePaths.Add(effectivePath))
                    continue;

                await SynchronizePathAsync(targetRoot, effectivePath, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _pendingPaths[relativePath] = 0;
                _logger.LogWarning(
                    ex,
                    "KI-Spiegel: Änderung wird später erneut versucht: {RelativePath}",
                    relativePath);
            }
        }
    }

    private string GetEffectiveRelativePath(string relativePath)
    {
        var sourcePath = GetSafeSourcePath(relativePath);
        var sqliteSnapshots = new SqliteSnapshotCopyService();
        if (!sqliteSnapshots.IsCompanionOfSqliteDatabase(sourcePath))
            return relativePath;

        return Path.GetRelativePath(_sourceRoot, sourcePath[..^4]);
    }

    private async Task SynchronizeAllAsync(string targetRoot, CancellationToken ct)
    {
        await _operationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ValidateSourceAndTarget(targetRoot);
            EnsureTrustedTarget(targetRoot);
            BackupTargetPathGuard.EnsureTreeIsSafe(targetRoot);

            var expectedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var preserved in PreservedTargetFiles)
                expectedTargets.Add(preserved);

            var stats = new DirectoryMirror.MirrorStats();
            var mirror = new DirectoryMirror(versionsStandName: null);
            var source = new BackupSource(
                _sourceRoot,
                string.Empty,
                IsSourceDirectoryExcluded,
                IsSourceFileExcluded);

            _logger.LogInformation(
                "KI-Spiegel: inkrementeller Vollabgleich gestartet: {Source} -> {Target}",
                _sourceRoot,
                targetRoot);

            // Alles, was vor dem Start des Vollabgleichs gemeldet wurde, wird durch
            // diesen Lauf erfasst. Neue Ereignisse während des Laufs bleiben dagegen
            // in der Warteschlange und schließen das kleine Änderungsfenster sicher.
            _pendingPaths.Clear();
            await mirror.MirrorSourceAsync(
                    source,
                    targetRoot,
                    expectedTargets,
                    stats,
                    ct: ct)
                .ConfigureAwait(false);

            if (stats.Errors.Count > 0)
            {
                throw new IOException(
                    $"Der KI-Spiegel konnte {stats.Errors.Count} Datei(en) nicht sicher übernehmen. " +
                    stats.Errors[0]);
            }

            // Marker und Junction-Freiheit unmittelbar vor der destruktiven
            // Verwaistenbereinigung erneut pruefen; der Abgleich kann lange dauern.
            EnsureTrustedTarget(targetRoot);
            BackupTargetPathGuard.EnsureTreeIsSafe(targetRoot);
            mirror.RemoveOrphans(targetRoot, expectedTargets, stats);
            if (stats.Errors.Count > 0)
            {
                throw new IOException(
                    $"Der KI-Spiegel konnte {stats.Errors.Count} Zielpfad(e) nicht bereinigen. " +
                    stats.Errors[0]);
            }

            _targetRoot = targetRoot;
            _logger.LogInformation(
                "KI-Spiegel aktuell: {Copied} kopiert, {Unchanged} unverändert, {Deleted} entfernt, " +
                "{Verified} geprüft, {Databases} SQLite-Snapshot(s).",
                stats.Copied,
                stats.Unchanged,
                stats.Deleted,
                stats.Verified,
                stats.DatabasesSnapshotted);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task SynchronizePathAsync(
        string targetRoot,
        string relativePath,
        CancellationToken ct)
    {
        await _operationGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ValidateSourceAndTarget(targetRoot);
            EnsureTrustedTarget(targetRoot);

            var sourcePath = GetSafeSourcePath(relativePath);
            var sqliteSnapshots = new SqliteSnapshotCopyService();
            if (sqliteSnapshots.IsCompanionOfSqliteDatabase(sourcePath))
            {
                sourcePath = sourcePath[..^4];
                relativePath = Path.GetRelativePath(_sourceRoot, sourcePath);
            }

            // Marker, Manifest und Alt-Log sind ausschliesslich Ziel-Kontrolldateien.
            // Der Watcher darf gleichnamige Quelldateien weder auf sie schreiben noch
            // ihre Loeschung auf das Ziel uebertragen.
            if (PreservedTargetFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
                return;

            var sourceAttributes = ReadSourceAttributes(sourcePath);
            if (sourceAttributes is not null
                && (sourceAttributes.Value & FileAttributes.Directory) != 0)
            {
                _needsFullScan = true;
                _nextFullScanUtc = DateTime.MinValue;
                return;
            }

            var targetPath = GetSafeTargetPath(targetRoot, relativePath);
            BackupTargetPathGuard.EnsurePathIsSafe(targetRoot, targetPath);
            if (sourceAttributes is null)
            {
                DeleteTargetPath(targetRoot, targetPath);
                DeleteSqliteCompanions(targetRoot, targetPath);
                return;
            }

            if (HasReparsePointInSourcePath(sourcePath))
                return;

            var stats = new DirectoryMirror.MirrorStats();
            var mirror = new DirectoryMirror(versionsStandName: null);
            await mirror.MirrorFileAsync(
                    new BackupSingleFile(sourcePath, relativePath),
                    targetRoot,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    stats,
                    ct: ct)
                .ConfigureAwait(false);

            if (stats.Errors.Count > 0)
                throw new IOException(stats.Errors[0]);

            if (sqliteSnapshots.IsSqliteDatabase(sourcePath))
                DeleteSqliteCompanions(targetRoot, targetPath);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    internal static FileAttributes? ReadSourceAttributes(
        string sourcePath,
        Func<string, FileAttributes>? readAttributes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        readAttributes ??= File.GetAttributes;

        try
        {
            return readAttributes(sourcePath);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or NotSupportedException)
        {
            throw new InvalidDataException(
                $"KI-Quelldatei konnte nicht sicher geprueft werden: {sourcePath}",
                ex);
        }
    }

    private string ResolveTargetOrThrow()
    {
        var targetRoot = _targetResolver();
        if (string.IsNullOrWhiteSpace(targetRoot))
        {
            throw new DirectoryNotFoundException(
                $"Datenträger \"{KnowledgeMirrorTargetResolver.DefaultVolumeLabel}\" ist nicht angeschlossen.");
        }

        return NormalizeRoot(targetRoot);
    }

    private void ValidateSourceAndTarget(string targetRoot)
    {
        if (!Directory.Exists(_sourceRoot))
            throw new DirectoryNotFoundException($"KI-Quellordner nicht gefunden: {_sourceRoot}");

        var conflict = BackupTargetGuard.CheckSourceTargetConflict(targetRoot, new[] { _sourceRoot });
        if (conflict is not null)
            throw new InvalidOperationException(conflict);
    }

    private void EnsureTrustedTarget(string targetRoot)
    {
        BackupTargetPathGuard.EnsureRootIsSafe(targetRoot);
        Directory.CreateDirectory(targetRoot);
        BackupTargetPathGuard.EnsureRootIsSafe(targetRoot);
        var markerPath = BackupTargetPathGuard.ResolveRelativePath(targetRoot, MarkerFileName);
        if (File.Exists(markerPath))
        {
            BackupTargetPathGuard.EnsurePathIsSafe(targetRoot, markerPath);
            var marker = File.ReadAllText(markerPath);
            KnowledgeMirrorMarker.Validate(marker, _sourceRoot, targetRoot);
            return;
        }

        var entries = Directory.EnumerateFileSystemEntries(targetRoot).Take(2).ToArray();
        if (entries.Length > 0 && !IsVerifiedLegacyMirror(targetRoot))
        {
            throw new InvalidDataException(
                $"Der Ordner \"{targetRoot}\" enthält Daten, aber keinen gültigen KI-Spiegel-Marker. " +
                "Aus Sicherheitsgründen wurde nichts verändert.");
        }

        var content = new StringBuilder()
            .AppendLine(KnowledgeMirrorMarker.Header)
            .AppendLine($"Source={_sourceRoot}")
            .AppendLine($"Target={targetRoot}")
            .AppendLine($"VolumeLabel={KnowledgeMirrorTargetResolver.DefaultVolumeLabel}")
            .ToString();
        var temporaryPath = markerPath + DirectoryMirror.TempSuffix;
        BackupTargetPathGuard.EnsurePathIsSafe(targetRoot, temporaryPath);
        File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        BackupTargetPathGuard.EnsurePathIsSafe(targetRoot, temporaryPath);
        BackupTargetPathGuard.EnsurePathIsSafe(targetRoot, markerPath);
        File.Move(temporaryPath, markerPath, overwrite: true);
        BackupTargetPathGuard.EnsurePathIsSafe(targetRoot, markerPath);
        File.SetAttributes(markerPath, FileAttributes.Hidden);
    }

    private bool IsVerifiedLegacyMirror(string targetRoot)
    {
        var logPath = Path.Combine(targetRoot, "_spiegel_log.txt");
        if (!File.Exists(logPath))
            return false;

        try
        {
            BackupTargetPathGuard.EnsurePathIsSafe(targetRoot, logPath);
            var log = File.ReadAllText(logPath);
            return KnowledgeMirrorMarker.MatchesLegacyLog(log, _sourceRoot, targetRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool IsSourceDirectoryExcluded(string relativePath)
        => IsReparsePoint(Path.Combine(_sourceRoot, relativePath));

    private bool IsSourceFileExcluded(string relativePath)
    {
        if (PreservedTargetFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
            return true;

        return IsReparsePoint(Path.Combine(_sourceRoot, relativePath));
    }

    private bool HasReparsePointInSourcePath(string sourcePath)
    {
        var current = sourcePath;
        while (!string.Equals(current, _sourceRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (IsReparsePoint(current))
                return true;

            current = Path.GetDirectoryName(current)
                      ?? throw new InvalidDataException($"Ungültiger Quellpfad: {sourcePath}");
        }

        return false;
    }

    private string GetSafeSourcePath(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(_sourceRoot, relativePath));
        if (!IsInsideRoot(_sourceRoot, path))
            throw new InvalidDataException($"Quellpfad liegt außerhalb von {_sourceRoot}: {relativePath}");
        return path;
    }

    private static string GetSafeTargetPath(string targetRoot, string relativePath)
        => BackupTargetPathGuard.ResolveRelativePath(targetRoot, relativePath);

    private bool TryGetRelativeSourcePath(string fullPath, out string relativePath)
    {
        var path = Path.GetFullPath(fullPath);
        if (!IsInsideRoot(_sourceRoot, path))
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = Path.GetRelativePath(_sourceRoot, path);
        return !string.IsNullOrWhiteSpace(relativePath) && relativePath != ".";
    }

    private static bool IsInsideRoot(string root, string path)
        => path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private void DeleteTargetPath(string targetRoot, string targetPath)
    {
        EnsureTrustedTarget(targetRoot);
        var markerPath = BackupTargetPathGuard.ResolveRelativePath(targetRoot, MarkerFileName);
        if (!File.Exists(markerPath))
            throw new InvalidDataException("KI-Spiegel-Marker fehlt. Löschen wurde blockiert.");
        if (!BackupTargetGuard.IsInsideBackupRoot(targetRoot, targetPath))
            throw new InvalidDataException("Zielpfad liegt außerhalb des KI-Spiegels.");
        BackupTargetPathGuard.EnsurePathIsSafe(targetRoot, targetPath);

        if (File.Exists(targetPath))
        {
            BackupTargetPathGuard.EnsurePathIsSafe(targetRoot, targetPath);
            File.Delete(targetPath);
        }
        else if (Directory.Exists(targetPath))
        {
            BackupTargetPathGuard.EnsureTreeIsSafe(targetPath);
            BackupTargetPathGuard.EnsurePathIsSafe(targetRoot, targetPath);
            Directory.Delete(targetPath, recursive: true);
        }
    }

    private void DeleteSqliteCompanions(string targetRoot, string targetDatabasePath)
    {
        EnsureTrustedTarget(targetRoot);
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var companion = targetDatabasePath + suffix;
            if (BackupTargetGuard.IsInsideBackupRoot(targetRoot, companion) && File.Exists(companion))
            {
                BackupTargetPathGuard.EnsurePathIsSafe(targetRoot, companion);
                File.Delete(companion);
            }
        }
    }

    private void LogTargetUnavailable(string message)
    {
        _targetRoot = null;
        _needsFullScan = true;
        if (_targetMissingWasLogged)
            return;

        _targetMissingWasLogged = true;
        _logger.LogWarning("KI-Spiegel wartet: {Message}", message);
    }

    private static string NormalizeRoot(string path)
        => Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _watcher?.Dispose();
            _watcher = null;
            _lifetime.Cancel();
        }

        var completed = true;
        try
        {
            completed = _worker?.Wait(TimeSpan.FromSeconds(3)) ?? true;
            if (!completed)
            {
                _logger.LogWarning(
                    "KI-Spiegel: Hintergrundarbeit reagierte beim Programmende nicht innerhalb von 3 Sekunden.");
            }
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            // Normaler Programmabschluss.
        }
        finally
        {
            if (completed)
            {
                _lifetime.Dispose();
                _operationGate.Dispose();
            }
        }
    }
}
