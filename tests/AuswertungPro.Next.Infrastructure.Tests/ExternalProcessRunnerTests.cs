using System.Diagnostics;
using System.Text;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ExternalProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_KillsProcess_WhenTimeoutIsReached()
    {
        var sw = Stopwatch.StartNew();

        var result = await ExternalProcessRunner.RunAsync(
            "powershell.exe",
            ["-NoProfile", "-Command", "Start-Sleep -Seconds 5"],
            TimeSpan.FromMilliseconds(250),
            Encoding.UTF8,
            Encoding.UTF8);

        sw.Stop();

        Assert.False(result.Success);
        Assert.True(result.TimedOut);
        Assert.Contains("Timeout", result.Message);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"Timeout dauerte zu lange: {sw.Elapsed}");
    }

    [Fact]
    public async Task RunAsync_CapturesStdoutAndStderr_WhenProcessExitsWithError()
    {
        var result = await ExternalProcessRunner.RunAsync(
            "powershell.exe",
            ["-NoProfile", "-Command", "[Console]::Out.WriteLine('stdout-ok'); [Console]::Error.WriteLine('stderr-ok'); exit 7"],
            TimeSpan.FromSeconds(5),
            Encoding.UTF8,
            Encoding.UTF8);

        Assert.False(result.Success);
        Assert.False(result.TimedOut);
        Assert.Equal(7, result.ExitCode);
        Assert.Contains("stdout-ok", result.StdOut);
        Assert.Contains("stderr-ok", result.StdErr);
        Assert.Contains("ExitCode 7", result.Message);
    }
}
