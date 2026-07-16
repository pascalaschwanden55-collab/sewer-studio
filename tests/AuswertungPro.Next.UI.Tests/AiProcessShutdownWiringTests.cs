namespace AuswertungPro.Next.UI.Tests;

using System.Reflection;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Startup;
using System.IO;
using Microsoft.Extensions.Logging;
using static TestRepoPaths;

public sealed class AiProcessShutdownWiringTests
{
    [Fact]
    public void ServiceProvider_Start_und_AppExit_verwenden_denselben_Prozessdienst()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        var launcher = new DefaultAiStartupLauncher(services.AiStartedProcesses);
        var lifetimeField = typeof(DefaultAiStartupLauncher).GetField(
            "_startedProcesses",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var appSource = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "App.xaml.cs"));

        Assert.NotNull(lifetimeField);
        Assert.Same(services.AiStartedProcesses, lifetimeField!.GetValue(launcher));
        Assert.Same(
            services.AiStartedProcesses,
            services.GetService(typeof(IAiStartedProcessLifetime)));
        Assert.Contains("_services?.AiStartedProcesses.StopAllStartedProcesses()", appSource, StringComparison.Ordinal);
        Assert.Contains("services.AiStartedProcesses,", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AiStartedProcessLifetime.StopAllStartedProcesses()", appSource, StringComparison.Ordinal);

        var before = AiStartedProcessLifetime.Current;
        var use = typeof(AiStartedProcessLifetime).GetMethod(
            "Use",
            BindingFlags.Static | BindingFlags.Public);
        Assert.NotNull(use);
        var error = Assert.Throws<TargetInvocationException>(() =>
            use!.Invoke(null, [services.AiStartedProcesses]));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, AiStartedProcessLifetime.Current);
    }
}
