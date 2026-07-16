using System.Reflection;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AiOptimizationSessionStoreDependencyTests
{
    [Fact]
    public void Sanierungsfabrik_verwendet_registrierten_Sitzungsspeicher_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var factory = services.DataPageSanierungViewModels;
        var field = factory.GetType().GetField(
            "_optimizationSessions",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Same(services.AiOptimizationSessions, field!.GetValue(factory));
        Assert.Same(
            services.AiOptimizationSessions,
            services.GetService(typeof(IAiOptimizationSessionStore)));

        var before = AiOptimizationSessionStore.Current;
        var use = typeof(AiOptimizationSessionStore).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.AiOptimizationSessions]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, AiOptimizationSessionStore.Current);
    }
}
