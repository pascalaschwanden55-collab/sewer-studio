using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Backup;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BackupManifestIntegrityDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Vollsicherung_verwenden_dieselbe_Instanz()
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
        Assert.Same(
            services.BackupManifestIntegrity,
            Infrastructure.Backup.BackupManifestIntegrity.Current);
        Assert.NotNull(field);
        Assert.Same(services.BackupManifestIntegrity, field.GetValue(services.FullBackup));
    }
}
