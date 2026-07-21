using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI;

public sealed partial class ServiceProvider
{
    private readonly FullBackupComposition _fullBackupComposition;

    public IKnowledgeWalCheckpoint KnowledgeWalCheckpoint { get; }

    public IBackupTargetMarkerGuard BackupTargetMarkers
        => _fullBackupComposition.TargetMarkers;

    public ISqliteSnapshotCopier SqliteSnapshots
        => _fullBackupComposition.SqliteSnapshots;

    public IBackupManifestIntegrityService BackupManifestIntegrity
        => _fullBackupComposition.ManifestIntegrity;

    public IFullBackupSourcesProvider BackupSources { get; }

    public IFullBackupService FullBackup
        => _fullBackupComposition.FullBackup;
}
