using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class FfmpegExecutableLocatorDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Ffmpeg_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(services.FfmpegExecutables, FfmpegLocator.Current);
        Assert.Same(
            services.FfmpegExecutables,
            services.GetService(typeof(IFfmpegExecutableLocator)));
    }
}
