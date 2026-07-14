using AuswertungPro.Next.Application.Vsa;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderVsaDependencyTests
{
    [Fact]
    public void ServiceProvider_stellt_Vsa_Schattenprotokoll_zentral_bereit()
    {
        var property = typeof(ServiceProvider)
            .GetProperty(nameof(ServiceProvider.VsaShadowTelemetry));

        Assert.NotNull(property);
        Assert.Equal(typeof(IVsaShadowTelemetryWriter), property.PropertyType);
        Assert.False(property.CanWrite);
    }
}
