using System.Reflection;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class InspectionProtocolFileLocatorDependencyTests
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

        Assert.Same(services.InspectionProtocolFiles, DataPageProtocolPathResolver.CompatibilityService);
        Assert.Same(
            services.InspectionProtocolFiles,
            services.GetService(typeof(IInspectionProtocolFileLocator)));
    }

    [Theory]
    [InlineData(typeof(DataPageViewModel), "_inspectionProtocolFiles")]
    [InlineData(typeof(DataPagePrintController), "_inspectionProtocolFiles")]
    [InlineData(typeof(BuilderPageViewModel), "_inspectionProtocolFiles")]
    [InlineData(typeof(KarteViewModel), "_inspectionProtocolFiles")]
    public void Produktive_Aufrufer_halten_den_Application_Vertrag(Type type, string fieldName)
    {
        var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(IInspectionProtocolFileLocator), field!.FieldType);
    }
}
