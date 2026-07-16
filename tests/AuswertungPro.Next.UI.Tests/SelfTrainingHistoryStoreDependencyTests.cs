using System.Reflection;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingHistoryStoreDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_SelbsttrainingVerlauf_direkt_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.IsType<SelfTrainingHistoryFileStore>(services.SelfTrainingHistory);
        Assert.Same(
            services.SelfTrainingHistory,
            services.GetService(typeof(ISelfTrainingHistoryStore)));

        var before = SelfTrainingHistoryStore.Current;
        var use = typeof(SelfTrainingHistoryStore).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.SelfTrainingHistory]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, SelfTrainingHistoryStore.Current);
    }
}
