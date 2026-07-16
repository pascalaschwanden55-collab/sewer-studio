using System.Reflection;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DichtheitProtocolFileLocatorDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_DP_Pfadsuche_ohne_globalen_Umschalter()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.DichtheitProtocolFiles,
            services.GetService(typeof(IDichtheitProtocolFileLocator)));
        Assert.Null(typeof(DataPageDichtheitPdfResolver).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.NonPublic));
    }

    [Fact]
    public void DP_Controller_haelt_nur_den_Application_Vertrag()
    {
        var field = typeof(DataPageDichtheitPdfController).GetField(
            "_files",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(IDichtheitProtocolFileLocator), field!.FieldType);
    }

    [Fact]
    public void Datenseite_delegiert_an_den_DP_Controller()
    {
        var field = typeof(DataPageViewModel).GetField(
            "_dichtheitPdfController",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(DataPageDichtheitPdfController), field!.FieldType);
    }
}
