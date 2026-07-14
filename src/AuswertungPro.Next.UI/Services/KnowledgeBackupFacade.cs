using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Services;

public interface IKnowledgeBackupService
{
    Task<KnowledgeBackupService.BackupResult> ExportAsync(
        string zipPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    Task<KnowledgeBackupService.BackupResult> ImportAsync(
        string zipPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Kompatible statische API fuer bestehende Aufrufer.
/// Neue Laufzeitpfade verwenden den zentralen IKnowledgeBackupService.
/// </summary>
public static class KnowledgeBackupService
{
    public sealed record BackupResult(bool Success, string? Error, int FileCount, long SizeBytes);

    public static Task<BackupResult> ExportAsync(
        string zipPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => new KnowledgeBackupTransferService().ExportAsync(zipPath, progress, ct);

    public static Task<BackupResult> ImportAsync(
        string zipPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => new KnowledgeBackupTransferService().ImportAsync(zipPath, progress, ct);
}

/// <summary>
/// Serialisiert Export und Import, damit zwei gleichzeitige Sicherungsvorgaenge
/// nicht dieselben Wissens- oder Einstellungsdateien gegeneinander austauschen.
/// </summary>
public sealed class KnowledgeBackupTransferService : IKnowledgeBackupService
{
    private readonly KnowledgeBackupLocations _locations;
    private readonly Action _flushPendingSettings;
    private readonly Action<IProgress<string>?> _flushSqliteWal;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public KnowledgeBackupTransferService()
        : this(
            KnowledgeBackupLocations.FromCurrentSystem(),
            AppSettings.FlushPendingSave,
            KnowledgeBackupEngine.FlushSqliteWal)
    {
    }

    internal KnowledgeBackupTransferService(
        KnowledgeBackupLocations locations,
        Action flushPendingSettings,
        Action<IProgress<string>?> flushSqliteWal)
    {
        _locations = locations ?? throw new ArgumentNullException(nameof(locations));
        _flushPendingSettings = flushPendingSettings
            ?? throw new ArgumentNullException(nameof(flushPendingSettings));
        _flushSqliteWal = flushSqliteWal
            ?? throw new ArgumentNullException(nameof(flushSqliteWal));
    }

    public Task<KnowledgeBackupService.BackupResult> ExportAsync(
        string zipPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => RunExclusiveAsync(
            token => KnowledgeBackupEngine.ExportAsync(
                zipPath,
                _locations,
                _flushPendingSettings,
                _flushSqliteWal,
                progress,
                token),
            ct);

    public Task<KnowledgeBackupService.BackupResult> ImportAsync(
        string zipPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => RunExclusiveAsync(
            token => KnowledgeBackupEngine.ImportAsync(
                zipPath,
                _locations,
                _flushPendingSettings,
                progress,
                token),
            ct);

    private async Task<KnowledgeBackupService.BackupResult> RunExclusiveAsync(
        Func<CancellationToken, Task<KnowledgeBackupService.BackupResult>> operation,
        CancellationToken ct)
    {
        await _operationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await operation(ct).ConfigureAwait(false);
        }
        finally
        {
            _operationLock.Release();
        }
    }
}
