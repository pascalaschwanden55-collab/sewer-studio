using System.Reflection;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderImportRunReportDependencyTests
{
    [Fact]
    public void ServiceProvider_and_import_page_use_the_instance_contract()
    {
        var property = typeof(ServiceProvider).GetProperty(nameof(ServiceProvider.ImportRunReports));
        var field = typeof(ImportPageViewModel).GetField(
            "_importRunReports",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(property);
        Assert.Equal(typeof(IImportRunReportExporter), property.PropertyType);
        Assert.False(property.CanWrite);
        Assert.NotNull(field);
        Assert.Equal(typeof(IImportRunReportExporter), field.FieldType);
    }
}
