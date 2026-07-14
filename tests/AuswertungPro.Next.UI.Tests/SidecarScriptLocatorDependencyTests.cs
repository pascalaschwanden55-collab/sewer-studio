using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Startup;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SidecarScriptLocatorDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Sidecar_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.SidecarScripts, SidecarScriptLocator.Current);
        Assert.Same(
            services.SidecarScripts,
            services.GetService(typeof(ISidecarScriptLocator)));
    }
}
