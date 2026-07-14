using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PdfMergeServiceDependencyTests
{
    [Fact]
    public void ServiceProvider_Datenseite_und_Druckcenter_verwenden_dieselbe_Instanz()
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
        using var dataPage = new DataPageViewModel(shell, services);
        using var builderPage = new BuilderPageViewModel(shell, services);

        var printControllerField = typeof(DataPageViewModel).GetField(
            "_printController",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var pdfMergeField = typeof(DataPagePrintController).GetField(
            "_pdfMerge",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var builderMergeField = typeof(BuilderPageViewModel).GetField(
            "_pdfMerge",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var printController = printControllerField?.GetValue(dataPage);
        Assert.NotNull(printController);
        Assert.NotNull(pdfMergeField);
        Assert.NotNull(builderMergeField);
        Assert.Same(services.PdfMerge, pdfMergeField!.GetValue(printController));
        Assert.Same(services.PdfMerge, builderMergeField!.GetValue(builderPage));
        Assert.Same(services.PdfMerge, PdfMergeHelper.Current);
        Assert.Same(
            services.PdfMerge,
            services.GetService(typeof(IPdfMergeService)));
    }
}
