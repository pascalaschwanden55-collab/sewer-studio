using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AiSettingsResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_und_EinstellungsFassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<AiPlatformSettingsResolver>(services.AiSettings);
        Assert.Same(
            services.AiSettings,
            services.GetService(typeof(IAiPlatformSettingsResolver)));
        Assert.Same(services.AiSettings, AiSettingsFactory.Current);
    }
}
