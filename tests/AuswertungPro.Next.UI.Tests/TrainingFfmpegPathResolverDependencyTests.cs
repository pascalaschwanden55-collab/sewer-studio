using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingFfmpegPathResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_und_Training_Fassade_verwenden_dieselbe_Instanz()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.TrainingFfmpegPaths,
            TrainingFfmpegPathResolver.CompatibilityService);
        Assert.Same(
            services.TrainingFfmpegPaths,
            services.GetService(typeof(ITrainingFfmpegPathResolver)));
    }
}
