using System.Reflection;
using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.UI.Mapping;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OfflineBasemapPathResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_Offlinekarten_Pfadsuche_ohne_globalen_Umschalter()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.OfflineBasemapPaths,
            services.GetService(typeof(IOfflineBasemapPathResolver)));
        Assert.Null(typeof(OfflineBasemapBaseResolver).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.NonPublic));
    }

    [Fact]
    public void Karten_ViewModel_haelt_den_Application_Vertrag()
    {
        var field = typeof(KarteViewModel).GetField(
            "_offlineBasemapPaths",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(IOfflineBasemapPathResolver), field!.FieldType);
    }
}
