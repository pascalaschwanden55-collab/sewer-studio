using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.UI.Mapping;
using AuswertungPro.Next.UI.QgisBridge;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KatasterXtfPathResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_Kataster_Pfadsuche_ohne_globalen_Umschalter()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.KatasterXtfPaths,
            services.GetService(typeof(IKatasterXtfPathResolver)));
        Assert.Null(typeof(KatasterXtfPathResolver).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.NonPublic));
    }

    [Fact]
    public void Kataster_Dienste_sind_direkt_verdrahtet_und_Fassaden_bleiben_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var exportPage = new ExportPageViewModel(shell, services);

        Assert.Same(
            services.HaltungCadastreTables,
            services.GetService(typeof(IHaltungCadastreTableStore)));
        Assert.Same(
            services.HaltungCadastreIndexes,
            services.GetService(typeof(IHaltungCadastreIndexProvider)));

        var field = typeof(ExportPageViewModel).GetField(
            "_haltungCadastreIndexes",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.Equal(typeof(IHaltungCadastreIndexProvider), field!.FieldType);
        Assert.Same(services.HaltungCadastreIndexes, field.GetValue(exportPage));

        var tableBefore = HaltungCadastreExtractor.Current;
        var tableUse = typeof(HaltungCadastreExtractor).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(tableUse);
        var tableError = Assert.Throws<TargetInvocationException>(() =>
            tableUse!.Invoke(null, [services.HaltungCadastreTables]));
        Assert.IsType<NotSupportedException>(tableError.InnerException);
        Assert.Same(tableBefore, HaltungCadastreExtractor.Current);

        var indexBefore = HaltungCadastreIndex.CurrentProvider;
        var indexUse = typeof(HaltungCadastreIndex).GetMethod(
            "UseProvider",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(indexUse);
        var indexError = Assert.Throws<TargetInvocationException>(() =>
            indexUse!.Invoke(null, [services.HaltungCadastreIndexes]));
        Assert.IsType<NotSupportedException>(indexError.InnerException);
        Assert.Same(indexBefore, HaltungCadastreIndex.CurrentProvider);
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
