using System.Reflection;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AiOptimizationSessionStoreDependencyTests
{
    [Fact]
    public void Datenseite_verwendet_registrierten_Sitzungsspeicher_und_Fassade_bleibt_unveraenderlich()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var shell = new ShellViewModel(
            services,
            new SystemMonitorService(enableHardwareSensorInit: false));
        using var dataPage = new DataPageViewModel(shell, services);
        var field = typeof(DataPageViewModel).GetField(
            "_aiOptimizationSessions",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Same(services.AiOptimizationSessions, field!.GetValue(dataPage));
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
