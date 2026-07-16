using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Backup;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BackupManifestIntegrityDependencyTests
{
    [Fact]
    public void ServiceProvider_verdrahtet_Sicherungspruefung_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var field = typeof(FullBackupService).GetField(
            "_manifestIntegrity",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsType<BackupManifestIntegrityService>(services.BackupManifestIntegrity);
        Assert.Same(
            services.BackupManifestIntegrity,
            services.GetService(typeof(IBackupManifestIntegrityService)));
        Assert.NotNull(field);
        Assert.Same(services.BackupManifestIntegrity, field.GetValue(services.FullBackup));

        var before = Infrastructure.Backup.BackupManifestIntegrity.Current;
        var use = typeof(Infrastructure.Backup.BackupManifestIntegrity).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.BackupManifestIntegrity]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, Infrastructure.Backup.BackupManifestIntegrity.Current);
    }
}
