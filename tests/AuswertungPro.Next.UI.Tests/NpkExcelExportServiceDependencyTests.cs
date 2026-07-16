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
    public void ServiceProvider_verdrahtet_NPK_Export_direkt_und_Fassade_bleibt_unveraenderlich()
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
        Assert.Same(
            services.NpkExcelExport,
            services.GetService(typeof(INpkLeistungsverzeichnisExcelExporter)));

        var before = NpkLeistungsverzeichnisExcelExporter.Current;
        var use = typeof(NpkLeistungsverzeichnisExcelExporter).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.NpkExcelExport]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, NpkLeistungsverzeichnisExcelExporter.Current);
    }
}
