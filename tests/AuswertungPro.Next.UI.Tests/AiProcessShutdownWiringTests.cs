namespace AuswertungPro.Next.UI.Tests;

using System.IO;
using static TestRepoPaths;

public sealed class AiProcessShutdownWiringTests
{
    [Fact]
    public void App_exit_stops_only_registered_ai_processes()
    {
        var appSource = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "App.xaml.cs"));
        var launcherSource = File.ReadAllText(
            RepoFile(
                "src",
                "AuswertungPro.Next.Infrastructure",
                "Ai",
                "Startup",
                "DefaultAiStartupLauncher.cs"));

        Assert.Contains("AiStartedProcessLifetime.StopAllStartedProcesses()", appSource, StringComparison.Ordinal);
        Assert.Contains("AiStartedProcessLifetime.TryTrack(process", launcherSource, StringComparison.Ordinal);
    }
}
