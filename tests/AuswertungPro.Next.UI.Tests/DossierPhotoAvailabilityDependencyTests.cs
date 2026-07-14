using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierPhotoAvailabilityDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Druckcenter_verwenden_denselben_Dienst()
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
        using var builder = new BuilderPageViewModel(shell, services);
        var field = typeof(BuilderPageViewModel).GetField(
            "_dossierPhotoAvailability",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Same(services.DossierPhotoAvailability, field!.GetValue(builder));
        Assert.Same(
            services.DossierPhotoAvailability,
            services.GetService(typeof(IDossierPhotoAvailabilityService)));
    }

    [Fact]
    public void Dossier_Druckcontroller_hält_den_Application_Vertrag()
    {
        var field = typeof(DataPagePrintController).GetField(
            "_dossierPhotoAvailability",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(IDossierPhotoAvailabilityService), field!.FieldType);
    }
}
