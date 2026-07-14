using AuswertungPro.Next.Application.Maintenance;
using AuswertungPro.Next.UI.Settings;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProgramRootLocatorDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Bereinigungsfassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.ProgramRootLocator,
            SettingsProgramCleanupRequestFactory.CompatibilityService);
        Assert.Same(
            services.ProgramRootLocator,
            services.GetService(typeof(IProgramRootLocator)));
    }
}
