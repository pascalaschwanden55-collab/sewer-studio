using System.Reflection;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderVideoStartErrorLogDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Datenseite_nutzen_den_Instanzvertrag()
    {
        var property = typeof(ServiceProvider)
            .GetProperty(nameof(ServiceProvider.VideoStartErrorLogs));
        var field = typeof(DataPageViewModel)
            .GetField("_videoStartErrorLogs", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(property);
        Assert.Equal(typeof(IVideoStartErrorLogWriter), property.PropertyType);
        Assert.False(property.CanWrite);
        Assert.NotNull(field);
        Assert.Equal(typeof(IVideoStartErrorLogWriter), field.FieldType);
    }
}
