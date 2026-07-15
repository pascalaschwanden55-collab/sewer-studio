using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Services;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FullBackupSourcesProviderDependencyTests
{
    [Fact]
    public void ServiceProvider_und_SicherungsquellenFassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<FullBackupSourcesProvider>(services.BackupSources);
        Assert.Same(
            services.BackupSources,
            services.GetService(typeof(IFullBackupSourcesProvider)));
        Assert.Same(services.BackupSources, FullBackupSourcesFactory.Current);
    }
}
