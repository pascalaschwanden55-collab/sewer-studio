using System.Diagnostics;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Startup;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AiStartedProcessLifetimeTests
{
    [Fact]
    public void Instanzdienst_beendet_den_von_ihm_verfolgten_Prozess()
    {
        if (!OperatingSystem.IsWindows())
            return;

        IAiStartedProcessLifetime lifetime = new AiStartedProcessLifetimeService();
        using var process = StartTestProcess();

        try
        {
            Assert.True(lifetime.TryTrack(process, out var error), error);

            lifetime.StopAllStartedProcesses();

            Assert.True(process.WaitForExit(milliseconds: 5_000));
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            lifetime.StopAllStartedProcesses();
        }
    }

    [Fact]
    public void Instanzdienst_beendet_keinen_fremd_verfolgten_Prozess()
    {
        if (!OperatingSystem.IsWindows())
            return;

        IAiStartedProcessLifetime ersterDienst = new AiStartedProcessLifetimeService();
        IAiStartedProcessLifetime zweiterDienst = new AiStartedProcessLifetimeService();
        using var ersterProzess = StartTestProcess();
        using var zweiterProzess = StartTestProcess();

        try
        {
            Assert.True(ersterDienst.TryTrack(ersterProzess, out var ersterFehler), ersterFehler);
            Assert.True(zweiterDienst.TryTrack(zweiterProzess, out var zweiterFehler), zweiterFehler);

            ersterDienst.StopAllStartedProcesses();

            Assert.True(ersterProzess.WaitForExit(milliseconds: 5_000));
            Assert.False(zweiterProzess.HasExited);
        }
        finally
        {
            ersterDienst.StopAllStartedProcesses();
            zweiterDienst.StopAllStartedProcesses();
            if (!ersterProzess.HasExited)
                ersterProzess.Kill(entireProcessTree: true);
            if (!zweiterProzess.HasExited)
                zweiterProzess.Kill(entireProcessTree: true);
        }
    }

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

    private static Process StartTestProcess()
        => Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-NoProfile -Command Start-Sleep -Seconds 30",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        }) ?? throw new InvalidOperationException("Testprozess konnte nicht gestartet werden.");
}
