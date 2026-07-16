using System.Reflection;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.Services;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCatalogPathResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_VSA_Katalogsuche_ohne_globalen_Umschalter()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.VsaCatalogPaths,
            services.GetService(typeof(IVsaCatalogPathResolver)));
        Assert.Null(typeof(VsaCatalogPathResolver).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.NonPublic));
    }
}
