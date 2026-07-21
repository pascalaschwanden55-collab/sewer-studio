using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Baut alle technischen Bausteine der Vollsicherung genau einmal zusammen.
/// Der Aufrufer liefert nur Quellen, WAL-Checkpoint, Ollama-Abfrage und Git-Aufloesung.
/// </summary>
public sealed class FullBackupComposition
{
    private FullBackupComposition(
        IBackupTargetMarkerGuard targetMarkers,
        ISqliteSnapshotCopier sqliteSnapshots,
        IBackupManifestIntegrityService manifestIntegrity,
        IFullBackupService fullBackup)
    {
        TargetMarkers = targetMarkers;
        SqliteSnapshots = sqliteSnapshots;
        ManifestIntegrity = manifestIntegrity;
        FullBackup = fullBackup;
    }

    public IBackupTargetMarkerGuard TargetMarkers { get; }

    public ISqliteSnapshotCopier SqliteSnapshots { get; }

    public IBackupManifestIntegrityService ManifestIntegrity { get; }

    public IFullBackupService FullBackup { get; }

    public static FullBackupComposition Create(
        Func<FullBackupSources> sourcesFactory,
        IKnowledgeWalCheckpoint walCheckpoint,
        Func<CancellationToken, Task<string?>> ollamaList,
        IGitCommitResolver gitCommitResolver)
    {
        ArgumentNullException.ThrowIfNull(sourcesFactory);
        ArgumentNullException.ThrowIfNull(walCheckpoint);
        ArgumentNullException.ThrowIfNull(ollamaList);
        ArgumentNullException.ThrowIfNull(gitCommitResolver);

        var targetMarkers = new BackupTargetMarkerGuardService();
        var sqliteSnapshots = new SqliteSnapshotCopyService();
        var manifestIntegrity = new BackupManifestIntegrityService();
        var fullBackup = new FullBackupService(
            sourcesFactory,
            walCheckpoint.TryCheckpoint,
            ollamaList,
            availableBytes: null,
            gitCommitResolver,
            targetMarkers,
            sqliteSnapshots,
            manifestIntegrity);

        return new FullBackupComposition(
            targetMarkers,
            sqliteSnapshots,
            manifestIntegrity,
            fullBackup);
    }
}
