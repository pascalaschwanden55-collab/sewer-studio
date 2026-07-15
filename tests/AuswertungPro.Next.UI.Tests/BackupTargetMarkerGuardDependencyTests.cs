using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Backup;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BackupTargetMarkerGuardDependencyTests
{
    [Fact]
    public void ServiceProvider_und_BackupFassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<BackupTargetMarkerGuardService>(services.BackupTargetMarkers);
        Assert.Same(services.BackupTargetMarkers, BackupTargetGuard.MarkerGuard);
        Assert.Same(
            services.BackupTargetMarkers,
            services.GetService(typeof(IBackupTargetMarkerGuard)));
    }
}
