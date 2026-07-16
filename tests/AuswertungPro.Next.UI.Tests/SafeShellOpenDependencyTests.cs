using System.Reflection;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Common;
using AuswertungPro.Next.UI.Services;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SafeShellOpenDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_den_sicheren_Oeffnungsdienst_direkt()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.ShellOpen,
            services.GetService(typeof(ISafeShellOpenService)));
    }

    [Fact]
    public void KompatibilitaetsFassade_kann_den_Dienst_nicht_mehr_global_austauschen()
    {
        var before = SafeShellOpen.CompatibilityService;
        var use = typeof(SafeShellOpen).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(
            () => use.Invoke(null, [new SafeShellOpenService()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, SafeShellOpen.CompatibilityService);
    }
}
