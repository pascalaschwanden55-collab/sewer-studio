using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Services;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FullBackupSourcesProviderDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_die_Sicherungsquellen_ohne_veraenderbaren_globalen_Umschalter()
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
        var use = typeof(FullBackupSourcesFactory).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(
            () => use.Invoke(null, new object?[] { FullBackupSourcesFactory.Current }));
        Assert.IsType<NotSupportedException>(error.InnerException);
    }
}
