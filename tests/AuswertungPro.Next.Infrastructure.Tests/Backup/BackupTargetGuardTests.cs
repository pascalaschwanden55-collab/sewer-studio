using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class BackupTargetGuardTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sewerstudio-backup-guard-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void ValidateAndCreateMarker_LeererOrdner_LegtMarkerAn()
    {
        var backupRoot = Path.Combine(_root, "backup");
        Directory.CreateDirectory(backupRoot);

        var error = BackupTargetGuard.ValidateAndCreateMarker(backupRoot);

        Assert.Null(error);
        Assert.True(File.Exists(Path.Combine(backupRoot, BackupPlanBuilder.MarkerFileName)));
    }

    [Fact]
    public void ValidateAndCreateMarker_NichtExistierenderOrdner_LegtOrdnerUndMarkerAn()
    {
        var backupRoot = Path.Combine(_root, "missing");

        var error = BackupTargetGuard.ValidateAndCreateMarker(backupRoot);

        Assert.Null(error);
        Assert.True(Directory.Exists(backupRoot));
        Assert.True(File.Exists(Path.Combine(backupRoot, BackupPlanBuilder.MarkerFileName)));
    }

    [Fact]
    public void ValidateAndCreateMarker_FremdinhaltOhneMarker_Blockiert()
    {
        var backupRoot = Path.Combine(_root, "foreign");
        Directory.CreateDirectory(backupRoot);
        File.WriteAllText(Path.Combine(backupRoot, "private.txt"), "nicht loeschen");

        var error = BackupTargetGuard.ValidateAndCreateMarker(backupRoot);

        Assert.NotNull(error);
        Assert.Contains("Marker-Datei fehlt", error);
        Assert.True(File.Exists(Path.Combine(backupRoot, "private.txt")));
    }

    [Fact]
    public void ValidateAndCreateMarker_MitMarker_IstOkTrotzInhalt()
    {
        var backupRoot = Path.Combine(_root, "existing");
        Directory.CreateDirectory(backupRoot);
        File.WriteAllText(Path.Combine(backupRoot, BackupPlanBuilder.MarkerFileName), "marker");
        File.WriteAllText(Path.Combine(backupRoot, "old.txt"), "alt");

        var error = BackupTargetGuard.ValidateAndCreateMarker(backupRoot);

        Assert.Null(error);
    }

    [Fact]
    public void CheckSourceTargetConflict_ZielInQuelle_Blockiert()
    {
        var source = Path.Combine(_root, "source");
        var backupRoot = Path.Combine(source, "SewerStudio_Datensicherung");

        var error = BackupTargetGuard.CheckSourceTargetConflict(backupRoot, new[] { source });

        Assert.NotNull(error);
        Assert.Contains("Zielordner liegt innerhalb", error);
    }

    [Fact]
    public void CheckSourceTargetConflict_QuelleInZiel_Blockiert()
    {
        var backupRoot = Path.Combine(_root, "backup");
        var source = Path.Combine(backupRoot, "nested-source");

        var error = BackupTargetGuard.CheckSourceTargetConflict(backupRoot, new[] { source });

        Assert.NotNull(error);
        Assert.Contains("Sicherungsquelle", error);
    }

    [Fact]
    public void CheckSourceTargetConflict_GetrenntePfade_SindOk()
    {
        var backupRoot = Path.Combine(_root, "backup");
        var source = Path.Combine(_root, "source");

        var error = BackupTargetGuard.CheckSourceTargetConflict(backupRoot, new[] { source });

        Assert.Null(error);
    }

    [Fact]
    public void IsInsideBackupRoot_AkzeptiertNurUnterhalbDesBackupRoots()
    {
        var backupRoot = Path.Combine(_root, "backup");

        Assert.True(BackupTargetGuard.IsInsideBackupRoot(backupRoot, Path.Combine(backupRoot, "a", "file.txt")));
        Assert.False(BackupTargetGuard.IsInsideBackupRoot(backupRoot, backupRoot));
        Assert.False(BackupTargetGuard.IsInsideBackupRoot(backupRoot, Path.Combine(_root, "outside.txt")));
    }
}
