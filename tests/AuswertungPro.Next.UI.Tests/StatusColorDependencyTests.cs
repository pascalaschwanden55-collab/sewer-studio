using System.Reflection;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Theme;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class StatusColorDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_den_StatusfarbenDienst_direkt()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings(),
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.StatusColors,
            services.GetService(typeof(IStatusColorService)));
    }

    [Fact]
    public void KompatibilitaetsFassade_kann_den_Farbdienst_nicht_mehr_global_austauschen()
    {
        var before = StatusColors.Current;
        var property = typeof(StatusColors).GetProperty(nameof(StatusColors.Current));

        Assert.NotNull(property);
        var error = Assert.Throws<TargetInvocationException>(
            () => property.SetValue(null, new StatusColorService()));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, StatusColors.Current);
    }
}
