using System.Reflection;
using AuswertungPro.Next.Application.Vsa;
using AuswertungPro.Next.Infrastructure.Vsa;

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

    [Fact]
    public void Alte_Vsa_Fassade_bleibt_erhalten_aber_kann_den_Schreiber_nicht_mehr_austauschen()
    {
        var facadeType = typeof(VsaShadowTelemetryWriter);
        var current = facadeType.GetProperty(nameof(VsaShadowTelemetryWriter.Current));
        var use = facadeType.GetMethod("Use", BindingFlags.Static | BindingFlags.Public);

        Assert.NotNull(current);
        Assert.False(current.CanWrite);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(
            () => use.Invoke(null, new object?[] { VsaShadowTelemetryWriter.Current }));
        Assert.IsType<NotSupportedException>(error.InnerException);
    }
}
