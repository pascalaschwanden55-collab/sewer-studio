using System;
using System.Diagnostics;
using System.Threading;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Startup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Startup;

/// <summary>
/// Tests fuer die Prozessverfolgung mit Art/Pfad und PID-Reuse-Schutz (Paket 2/A3).
/// Echte Prozesse nur nach dem vorhandenen, sicheren Self-Test-Muster (kurzer
/// powershell-Sleep, wird in jedem Fall beendet).
/// </summary>
public sealed class AiStartedProcessLifetimeServiceTests
{
    // Plausibel unmoegliche PID: GetProcessById schlaegt garantiert fehl.
    private const int ImpossiblePid = 1_073_741_820;

    private static Process StartSleepProcess()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = "-NoProfile -Command Start-Sleep -Seconds 30",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
        Assert.NotNull(process);
        return process;
    }

    [Fact]
    public void TryTrack_mit_art_und_pfad_liefert_identitaetsdaten()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var lifetime = new AiStartedProcessLifetimeService();
        using var process = StartSleepProcess();
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
    public void Nur_ollama_getrackt_ist_kein_sidecar()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var lifetime = new AiStartedProcessLifetimeService();
        using var process = StartSleepProcess();
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
    public void Beendeter_prozess_wird_bei_abfrage_entfernt()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var lifetime = new AiStartedProcessLifetimeService();
        using var process = StartSleepProcess();
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
    public void Probe_laufender_prozess_liefert_startzeit_und_pfad()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var process = StartSleepProcess();
        try
        {
            var probe = ProcessTreeInspector.ProbeProcessIdentity(process.Id);

            Assert.True(probe.Found);
            Assert.Equal(process.StartTime.ToUniversalTime(), probe.StartTimeUtc);

            // MainModule ist direkt nach dem Start unter Last manchmal noch nicht lesbar
            // oder Windows meldet waehrend der Initialisierung kurz ntdll.dll: nachfassen.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while ((probe.ImagePath is null
                    || !probe.ImagePath.Contains("powershell", StringComparison.OrdinalIgnoreCase))
                   && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(50);
                probe = ProcessTreeInspector.ProbeProcessIdentity(process.Id);
            }

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
