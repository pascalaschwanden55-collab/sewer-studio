using System.Reflection;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterSettingsStoreDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_TrainingsEinstellungen_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<TrainingCenterSettingsFileStore>(services.TrainingSettings);
        Assert.Same(
            services.TrainingSettings,
            services.GetService(typeof(ITrainingCenterSettingsStore)));

        var before = TrainingCenterSettingsStore.Current;
        var use = typeof(TrainingCenterSettingsStore).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.TrainingSettings]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, TrainingCenterSettingsStore.Current);
    }
}
