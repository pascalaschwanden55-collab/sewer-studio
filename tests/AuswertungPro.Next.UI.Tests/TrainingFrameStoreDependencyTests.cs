using System.Reflection;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingFrameStoreDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_TrainingsFrames_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<TrainingFrameFileStore>(services.TrainingFrames);
        Assert.Same(
            services.TrainingFrames,
            services.GetService(typeof(ITrainingFrameStore)));

        var before = FrameStore.Current;
        var use = typeof(FrameStore).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.TrainingFrames]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, FrameStore.Current);
    }
}
