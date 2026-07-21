using System.IO;
using System.Reflection;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Backup;
using Microsoft.Extensions.Logging;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BackupTargetMarkerGuardDependencyTests
{
    [Fact]
    public void ServiceProvider_verdrahtet_den_Marker_direkt_ohne_globale_Fassade_zu_aendern()
    {
        var globalMarkerBefore = BackupTargetGuard.MarkerGuard;
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<BackupTargetMarkerGuardService>(services.BackupTargetMarkers);
        Assert.Same(globalMarkerBefore, BackupTargetGuard.MarkerGuard);
        Assert.NotSame(services.BackupTargetMarkers, BackupTargetGuard.MarkerGuard);
        Assert.Same(
            services.BackupTargetMarkers,
            services.GetService(typeof(IBackupTargetMarkerGuard)));

        var field = typeof(FullBackupService).GetField(
            "_targetMarkerGuard",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.Same(services.BackupTargetMarkers, field.GetValue(services.FullBackup));
    }

    [Fact]
    public void ServiceProvider_delegiert_den_Backup_Aufbau_an_die_Komposition()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ServiceProvider.cs"));

        Assert.Contains("FullBackupComposition.Create", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new FullBackupService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BackupTargetGuard.UseMarkerGuard", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new BackupTargetMarkerGuardService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new SqliteSnapshotCopyService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new BackupManifestIntegrityService", source, StringComparison.Ordinal);
    }
}
