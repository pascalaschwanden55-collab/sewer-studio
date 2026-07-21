using System.Reflection;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class FullBackupCompositionTests
{
    [Fact]
    public void Create_baut_ein_geschlossenes_Backup_Paket_ohne_globale_Fassade_zu_aendern()
    {
        var globalMarkerBefore = BackupTargetGuard.MarkerGuard;
        var sourcesFactoryCalls = 0;
        var walCheckpoint = new RecordingWalCheckpoint();
        var gitCommit = new RecordingGitCommitResolver();
        Func<FullBackupSources> sourcesFactory = () =>
        {
            sourcesFactoryCalls++;
            throw new InvalidOperationException("Die Quellen duerfen beim Aufbau noch nicht gelesen werden.");
        };
        Func<CancellationToken, Task<string?>> ollamaList = _ => Task.FromResult<string?>("[]");

        var composition = FullBackupComposition.Create(
            sourcesFactory,
            walCheckpoint,
            ollamaList,
            gitCommit);

        Assert.Equal(0, sourcesFactoryCalls);
        Assert.Same(globalMarkerBefore, BackupTargetGuard.MarkerGuard);
        Assert.IsType<BackupTargetMarkerGuardService>(composition.TargetMarkers);
        Assert.IsType<SqliteSnapshotCopyService>(composition.SqliteSnapshots);
        Assert.IsType<BackupManifestIntegrityService>(composition.ManifestIntegrity);

        var service = Assert.IsType<FullBackupService>(composition.FullBackup);
        Assert.Same(
            composition.TargetMarkers,
            ReadField<IBackupTargetMarkerGuard>(service, "_targetMarkerGuard"));
        Assert.Same(
            composition.SqliteSnapshots,
            ReadField<ISqliteSnapshotCopier>(service, "_sqliteSnapshots"));
        Assert.Same(
            composition.ManifestIntegrity,
            ReadField<IBackupManifestIntegrityService>(service, "_manifestIntegrity"));
        Assert.Same(
            gitCommit,
            ReadField<IGitCommitResolver>(service, "_gitCommitResolver"));
        Assert.Same(
            sourcesFactory,
            ReadField<Func<FullBackupSources>>(service, "_sourcesFactory"));
        Assert.Same(
            ollamaList,
            ReadField<Func<CancellationToken, Task<string?>>>(service, "_ollamaList"));

        var checkpointAction = ReadField<Action>(service, "_walCheckpoint");
        Assert.Same(walCheckpoint, checkpointAction.Target);
        Assert.Equal(nameof(IKnowledgeWalCheckpoint.TryCheckpoint), checkpointAction.Method.Name);
    }

    private static T ReadField<T>(FullBackupService service, string fieldName)
        where T : class
    {
        var field = typeof(FullBackupService).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        return Assert.IsAssignableFrom<T>(field.GetValue(service));
    }

    private sealed class RecordingWalCheckpoint : IKnowledgeWalCheckpoint
    {
        public void TryCheckpoint()
        {
        }
    }

    private sealed class RecordingGitCommitResolver : IGitCommitResolver
    {
        public string? Resolve(string? repoRoot) => "test-commit";
    }
}
