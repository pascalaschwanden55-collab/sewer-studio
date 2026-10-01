using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// "Katasterkennungen ergaenzen" haengt am Application-Vertrag und kommt aus dem
/// ServiceProvider — kein <c>new</c> in den Seiten, kein statischer Umschalter.
/// </summary>
public sealed class KatasterKennungLeserDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_den_Kennungsleser_als_Application_Vertrag()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.KatasterKennungen, services.GetService(typeof(IKatasterKennungLeser)));
        Assert.IsType<KatasterKennungGpkgLeser>(services.KatasterKennungen);
        Assert.Null(typeof(KatasterKennungGpkgLeser).GetMethod("Use", BindingFlags.Static | BindingFlags.Public));
    }

    // Der Pfad wird bei jedem Lauf frisch aus den Einstellungen gelesen, damit eine
    // Aenderung dort sofort greift.
    [Fact]
    public void Der_Kennungsleser_liest_den_Pfad_bei_jedem_Aufruf_aus_den_Einstellungen()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var settings = new AppSettings { EnableRestorePoints = false };
        var services = new ServiceProvider(
            settings, new DiagnosticsOptions(), loggerFactory.CreateLogger("test"), loggerFactory);

        Assert.Equal(AppSettings.DefaultKatasterKennungenGpkgPath, services.KatasterKennungen.Quellpfad());
        settings.KatasterKennungenGpkgPath = @"D:\anderswo\Kennungen.gpkg";
        Assert.Equal(@"D:\anderswo\Kennungen.gpkg", services.KatasterKennungen.Quellpfad());
    }

    [Fact]
    public void Die_Haltungsseite_haelt_den_Application_Vertrag()
    {
        var field = typeof(DataPageViewModel).GetField(
            "_katasterKennungen", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(IKatasterKennungLeser), field!.FieldType);
    }

    [Fact]
    public void Die_Schachtseite_haelt_den_Application_Vertrag()
    {
        var property = typeof(SchaechtePageViewModel).GetProperty(
            "KatasterKennungen", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(property);
        Assert.Equal(typeof(IKatasterKennungLeser), property!.PropertyType);
    }
}
