using System.Diagnostics;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProcessOutputReaderTests
{
    [Fact]
    public async Task Liest_Ausgabe_Fehlerausgabe_und_Rueckgabecode()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var startInfo = CreatePowerShellStartInfo(
            "[Console]::Out.Write('standard'); " +
            "[Console]::Error.Write('fehler'); exit 7");

        var result = await ProcessOutputReader.ReadToExitAsync(
            startInfo,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(7, result!.ExitCode);
        Assert.Equal("standard", result.StandardOutput);
        Assert.Equal("fehler", result.StandardError);
    }

    [Fact]
    public async Task Instanzdienst_liefert_die_Prozessausgabe()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var service = new ProcessOutputReaderService();
        var startInfo = CreatePowerShellStartInfo("[Console]::Out.Write('instanz')");

        var result = await service.ReadToExitAsync(startInfo, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0, result!.ExitCode);
        Assert.Equal("instanz", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    [Fact]
    public async Task Abbruch_beendet_den_gesamten_Prozessbaum()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var startInfo = CreatePowerShellStartInfo("Start-Sleep -Seconds 30");

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

    private static ProcessStartInfo CreatePowerShellStartInfo(string command)
    {
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
        startInfo.ArgumentList.Add(command);
        return startInfo;
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
