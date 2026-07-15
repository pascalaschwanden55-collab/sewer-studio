using System.Reflection;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class NpkExcelExportServiceDependencyTests
{
    [Fact]
    public void ServiceProvider_Fassade_und_Druckcenter_verwenden_dieselbe_Instanz()
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
        using var viewModel = new BuilderPageViewModel(shell, services);

        var field = typeof(BuilderPageViewModel).GetField(
            "_npkExcelExporter",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Same(services.NpkExcelExport, field!.GetValue(viewModel));
        Assert.Same(services.NpkExcelExport, NpkLeistungsverzeichnisExcelExporter.Current);
        Assert.Same(
            services.NpkExcelExport,
            services.GetService(typeof(INpkLeistungsverzeichnisExcelExporter)));
    }
}
