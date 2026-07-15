using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.UI.Mapping;
using AuswertungPro.Next.UI.QgisBridge;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KatasterXtfPathResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_und_statische_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.KatasterXtfPaths, KatasterXtfPathResolver.CompatibilityService);
        Assert.Same(
            services.KatasterXtfPaths,
            services.GetService(typeof(IKatasterXtfPathResolver)));
    }

    [Fact]
    public void Kataster_Index_und_Exportseite_verwenden_den_zentralen_Dienst()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.HaltungCadastreIndexes, HaltungCadastreIndex.CurrentProvider);
        Assert.Same(
            services.HaltungCadastreIndexes,
            services.GetService(typeof(IHaltungCadastreIndexProvider)));

        var field = typeof(ExportPageViewModel).GetField(
            "_haltungCadastreIndexes",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.Equal(typeof(IHaltungCadastreIndexProvider), field!.FieldType);
    }

    [Theory]
    [InlineData(typeof(ExportPageViewModel), "_katasterXtfPaths")]
    [InlineData(typeof(KarteViewModel), "_katasterXtfPaths")]
    [InlineData(typeof(SettingsPageViewModel), "_katasterXtfPaths")]
    [InlineData(typeof(QgisBridgeSnapshotBuilder), "_katasterXtfPaths")]
    public void Produktive_Aufrufer_halten_den_Application_Vertrag(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(IKatasterXtfPathResolver), field!.FieldType);
    }
}
