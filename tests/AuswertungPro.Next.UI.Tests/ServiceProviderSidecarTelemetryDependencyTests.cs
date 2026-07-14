using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ServiceProviderSidecarTelemetryDependencyTests
{
    [Fact]
    public void ServiceProvider_stellt_Sidecar_Telemetrie_zentral_bereit()
    {
        var property = typeof(ServiceProvider)
            .GetProperty(nameof(ServiceProvider.SidecarTelemetry));

        Assert.NotNull(property);
        Assert.Equal(typeof(ISidecarTelemetryWriter), property.PropertyType);
        Assert.False(property.CanWrite);
    }
}
