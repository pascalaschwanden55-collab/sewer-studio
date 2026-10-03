using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Startup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Startup;

/// <summary>
/// Tests fuer die Prozessverfolgung mit Art/Pfad und PID-Reuse-Schutz (Paket 2/A3).
/// Der eigene Testprozess meldet seine Bereitschaft und wartet auf Standardeingabe.
/// Er wird in jedem Fall beendet, ohne feste Schlafdauer als Lebenszeit.
/// </summary>
public sealed class AiStartedProcessLifetimeServiceTests
{
    // Plausibel unmoegliche PID: GetProcessById schlaegt garantiert fehl.
    private const int ImpossiblePid = 1_073_741_820;

    private static async Task<Process> StartTestProcessAsync()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-NoProfile -Command \"[Console]::Out.WriteLine('ready'); [Console]::Out.Flush(); [Console]::In.ReadLine() | Out-Null\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
        Assert.NotNull(process);
        try
        {
            // Erst nach der Antwort ist PowerShell vollstaendig initialisiert.
            Assert.Equal("ready", await process.StandardOutput.ReadLineAsync()
                .WaitAsync(TimeSpan.FromSeconds(5)));
            return process;
        }
        catch (Exception startupError)
        {
            try
            {
                // EOF gibt auch bei fehlgeschlagenem Kill den eigenen Testprozess frei.
                process.StandardInput.Close();
                if (!process.WaitForExit(milliseconds: 5_000))
                {
                    process.Kill(entireProcessTree: true);
                    Assert.True(process.WaitForExit(milliseconds: 5_000),
                        "Der eigene Testprozess wurde nicht beendet.");
                }
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Prozessstart und anschliessende Freigabe sind fehlgeschlagen.",
                    startupError, cleanupError);
            }
            finally
            {
                process.Dispose();
            }
            throw;
        }
    }

    [Fact]
    public async Task TryTrack_mit_art_und_pfad_liefert_identitaetsdaten()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var lifetime = new AiStartedProcessLifetimeService();
        using var process = await StartTestProcessAsync();
        try
        {
            Assert.True(
                lifetime.TryTrack(process, AiStartedProcessKind.Sidecar, "powershell", out var error),
                error);

            Assert.True(lifetime.HasTrackedStartedProcesses);
            Assert.True(lifetime.HasTrackedSidecarProcess);

            var info = lifetime.GetTrackedProcessInfo(process.Id);
            Assert.NotNull(info);
            Assert.Equal(process.Id, info!.ProcessId);
            Assert.Equal(AiStartedProcessKind.Sidecar, info.Kind);
            Assert.Equal("powershell", info.ExpectedImagePath);
            Assert.Equal(process.StartTime.ToUniversalTime(), info.StartTimeUtc);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            lifetime.StopAllStartedProcesses();
        }
    }

    [Fact]
    public async Task Nur_ollama_getrackt_ist_kein_sidecar()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var lifetime = new AiStartedProcessLifetimeService();
        using var process = await StartTestProcessAsync();
        try
        {
            Assert.True(
                lifetime.TryTrack(process, AiStartedProcessKind.Ollama, "ollama", out var error),
                error);

            Assert.True(lifetime.HasTrackedStartedProcesses);
            Assert.False(lifetime.HasTrackedSidecarProcess);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            lifetime.StopAllStartedProcesses();
        }
    }

    [Fact]
    public async Task Beendeter_prozess_wird_bei_abfrage_entfernt()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var lifetime = new AiStartedProcessLifetimeService();
        using var process = await StartTestProcessAsync();
        try
        {
            Assert.True(
                lifetime.TryTrack(process, AiStartedProcessKind.Sidecar, "powershell", out var error),
                error);
            Assert.True(lifetime.IsTrackedProcess(process.Id));

            process.Kill(entireProcessTree: true);
            Assert.True(process.WaitForExit(milliseconds: 5_000));

            // Veralteter Eintrag (Prozess beendet): darf nicht mehr als "eigener Prozess"
            // gelten — eine wiederverwendete PID wuerde sonst zum Falschpositiv.
            Assert.False(lifetime.IsTrackedProcess(process.Id));
            Assert.False(lifetime.HasTrackedStartedProcesses);
            Assert.False(lifetime.HasTrackedSidecarProcess);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            lifetime.StopAllStartedProcesses();
        }
    }

    // ── DefaultAiStartupLauncher.ClassifyKind ────────────────────────────────

    [Fact]
    public void ClassifyKind_erkennt_ollama_start()
    {
        var kind = DefaultAiStartupLauncher.ClassifyKind(
            new AiStartupProcessRequest("ollama", "serve", null, Hidden: true));

        Assert.Equal(AiStartedProcessKind.Ollama, kind);
    }

    [Fact]
    public void ClassifyKind_erkennt_sidecar_skript()
    {
        var kind = DefaultAiStartupLauncher.ClassifyKind(
            new AiStartupProcessRequest(
                @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -File \"C:\\repo\\sidecar\\start_sidecar.ps1\"",
                @"C:\repo\sidecar",
                Hidden: true));

        Assert.Equal(AiStartedProcessKind.Sidecar, kind);
    }

    [Fact]
    public void ClassifyKind_unbekannter_auftrag_bleibt_unknown()
    {
        var kind = DefaultAiStartupLauncher.ClassifyKind(
            new AiStartupProcessRequest("cmd", "/c echo hallo", null, Hidden: true));

        Assert.Equal(AiStartedProcessKind.Unknown, kind);
    }

    // ── ProcessTreeInspector: Probe/Kill ohne echte Ziele ────────────────────

    [Fact]
    public void Probe_unbekannte_pid_meldet_nicht_gefunden()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var probe = ProcessTreeInspector.ProbeProcessIdentity(ImpossiblePid);

        Assert.False(probe.Found);
        Assert.Null(probe.StartTimeUtc);
        Assert.Null(probe.ImagePath);
    }

    [Fact]
    public void Kill_unbekannte_pid_gilt_als_erreicht()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // Nichts zu beenden = Kill-Ziel erreicht (kein Doppelstart-Risiko).
        Assert.True(ProcessTreeInspector.KillProcessTree(ImpossiblePid, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Probe_laufender_prozess_liefert_startzeit_und_pfad()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var process = await StartTestProcessAsync();
        try
        {
            var probe = ProcessTreeInspector.ProbeProcessIdentity(process.Id);

            Assert.True(probe.Found);
            Assert.Equal(process.StartTime.ToUniversalTime(), probe.StartTimeUtc);

            Assert.NotNull(probe.ImagePath);
            Assert.Contains("powershell", probe.ImagePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
    }
}
