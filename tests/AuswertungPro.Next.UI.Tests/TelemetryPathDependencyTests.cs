using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TelemetryPathDependencyTests
{
    [Fact]
    public void ServiceProvider_und_PfadFassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<TelemetryFilePathResolver>(services.TelemetryPaths);
        Assert.Same(
            services.TelemetryPaths,
            services.GetService(typeof(ITelemetryPathResolver)));
        Assert.Same(services.TelemetryPaths, TelemetryPathResolver.Current);
    }
}
