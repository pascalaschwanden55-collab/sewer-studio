using System.Reflection;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingFfmpegPathResolverDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_FFmpeg_Pfadsuche_ohne_globalen_Umschalter()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.TrainingFfmpegPaths,
            services.GetService(typeof(ITrainingFfmpegPathResolver)));
        Assert.Null(typeof(TrainingFfmpegPathResolver).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.NonPublic));
    }
}
