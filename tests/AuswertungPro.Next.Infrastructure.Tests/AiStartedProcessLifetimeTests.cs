using System.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai.Startup;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AiStartedProcessLifetimeTests
{
    [Fact]
    public void StopAllStartedProcesses_ends_tracked_process()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-NoProfile -Command Start-Sleep -Seconds 30",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
        Assert.NotNull(process);

        try
        {
            Assert.True(AiStartedProcessLifetime.TryTrack(process!, out var error), error);

            AiStartedProcessLifetime.StopAllStartedProcesses();

            Assert.True(process!.WaitForExit(milliseconds: 5_000));
        }
        finally
        {
            if (process is { HasExited: false })
                process.Kill(entireProcessTree: true);
            AiStartedProcessLifetime.StopAllStartedProcesses();
        }
    }
}
