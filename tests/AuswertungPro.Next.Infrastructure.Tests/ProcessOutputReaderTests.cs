using System.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProcessOutputReaderTests
{
    [Fact]
    public async Task Abbruch_beendet_den_gesamten_Prozessbaum()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("Start-Sleep -Seconds 30");

        int? processId = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProcessOutputReader.ReadToExitAsync(
                startInfo,
                cts.Token,
                onStarted: id => processId = id));

        Assert.NotNull(processId);
        Assert.True(
            SpinWait.SpinUntil(() => !IsRunning(processId.Value), TimeSpan.FromSeconds(5)),
            $"Der abgebrochene Prozess {processId} laeuft weiter.");
    }

    private static bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
