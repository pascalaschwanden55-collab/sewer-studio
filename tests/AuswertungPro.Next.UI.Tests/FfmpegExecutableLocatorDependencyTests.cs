using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FfmpegExecutableLocatorDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_den_FfmpegFinder()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.FfmpegExecutables,
            services.GetService(typeof(IFfmpegExecutableLocator)));
    }

    [Fact]
    public void Statische_FfmpegFassade_ist_unveraenderbar()
    {
        var before = FfmpegLocator.Current;
        var use = typeof(FfmpegLocator).GetMethod(nameof(FfmpegLocator.Use));

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [new FfmpegFileLocator()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, FfmpegLocator.Current);
    }
}
