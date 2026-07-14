using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SidecarTokenResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Token_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.SidecarTokens, SidecarTokenResolver.Current);
        Assert.Same(
            services.SidecarTokens,
            services.GetService(typeof(ISidecarTokenResolver)));
    }
}
