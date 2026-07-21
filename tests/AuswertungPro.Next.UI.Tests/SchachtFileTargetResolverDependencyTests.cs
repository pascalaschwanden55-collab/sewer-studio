using System.Reflection;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtFileTargetResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_Schacht_Dateiziele_ohne_globalen_Umschalter()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.SchachtFileTargets,
            services.GetService(typeof(ISchachtFileTargetResolver)));
        Assert.Null(typeof(SchachtFileTargetResolver).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.NonPublic));
    }

    [Fact]
    public void Schacht_ViewModel_haelt_den_Application_Vertrag()
    {
        var field = typeof(SchaechtePageViewModel).GetField(
            "_schachtFileTargets",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(ISchachtFileTargetResolver), field!.FieldType);
    }

    [Fact]
    public void Schacht_ViewModel_verwendet_die_injizierte_Dateizielsuche()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        var resolver = new RecordingSchachtFileTargetResolver();
        var viewModel = new SchaechtePageViewModel(
            shell,
            services.Settings,
            services.Dialogs,
            services.SchachtProtocolImport,
            services.SchachtStammdatenErgaenzung,
            services.SchachtMassnahmenKatalog,
            services.CostStores.CreateProjectCostStore("schacht_empfehlungen.json"),
            services.DropdownOptions,
            services.PdfTextLayerRewrite,
            services.ShellOpen,
            services.ShaftRename,
            services.ExplorerReveal,
            services.SchaechteTemplateColumns,
            resolver);

        Assert.Same(resolver, viewModel.SchachtFileTargets);
    }

    private sealed class RecordingSchachtFileTargetResolver : ISchachtFileTargetResolver
    {
        public string? ResolvePdfPath(SchachtRecord record, string? projectFilePath) => null;

        public string? ResolveExplorerTarget(SchachtRecord record, string? projectFilePath) => null;
    }
}
