using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderSettingsFileDependencyTests
{
    [Fact]
    public void ServiceProvider_stellt_den_Settings_Dateispeicher_zentral_bereit()
    {
        var property = typeof(ServiceProvider)
            .GetProperty(nameof(ServiceProvider.SettingsFiles));

        Assert.NotNull(property);
        Assert.Equal(typeof(ISettingsFileStore), property.PropertyType);
        Assert.False(property.CanWrite);
    }
}
