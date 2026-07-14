using System.Diagnostics;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Startup;

/// <summary>
/// Verfolgt ausschliesslich KI-Prozesse, die SewerStudio selbst gestartet hat.
/// Bereits vorher laufende Ollama- oder Sidecar-Prozesse werden nie registriert oder beendet.
/// </summary>
public sealed class AiStartedProcessLifetimeService : IAiStartedProcessLifetime
{
    private readonly object _sync = new();
    private readonly List<TrackedProcess> _trackedProcesses = [];
    private WindowsKillOnCloseJob? _windowsJob;

    public bool TryTrack(Process process, out string? error)
    {
        ArgumentNullException.ThrowIfNull(process);

        lock (_sync)
        {
            var tracked = TryRememberProcess(process);
            if (!OperatingSystem.IsWindows())
            {
                error = tracked ? null : "KI-Prozess konnte nicht verfolgt werden.";
                return tracked;
            }

            try
            {
                _windowsJob ??= WindowsKillOnCloseJob.Create();
                _windowsJob.Add(process);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                BestEffort.ReportWarning(
                    $"[KI-Prozesse] Windows-Prozessgruppe nicht verfuegbar; " +
                    $"Rueckfall auf direkte Prozessbeendigung: {ex.GetType().Name}: {ex.Message}");
                error = tracked ? null : ex.Message;
                return tracked;
            }
        }
    }

    public void StopAllStartedProcesses()
    {
        WindowsKillOnCloseJob? job;
        TrackedProcess[] tracked;
        lock (_sync)
        {
            job = _windowsJob;
            _windowsJob = null;
            tracked = _trackedProcesses.ToArray();
            _trackedProcesses.Clear();
        }

        BestEffort.Try(
            () => job?.Dispose(),
            "[KI-Prozesse] Windows-Prozessgruppe schliessen");

        foreach (var item in tracked)
        {
            BestEffort.Try(
                () => KillIfSameProcess(item),
                $"[KI-Prozesse] Prozess {item.ProcessId} beenden");
        }
    }

    private bool TryRememberProcess(Process process)
    {
        try
        {
            _trackedProcesses.RemoveAll(item => item.ProcessId == process.Id);
            _trackedProcesses.Add(new TrackedProcess(process.Id, process.StartTime.ToUniversalTime()));
            return true;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[KI-Prozesse] Prozess {process.Id} konnte nicht fuer das Aufraeumen vorgemerkt werden: " +
                $"{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void KillIfSameProcess(TrackedProcess tracked)
    {
        using var process = Process.GetProcessById(tracked.ProcessId);
        if (process.HasExited || process.StartTime.ToUniversalTime() != tracked.StartTimeUtc)
            return;

        process.Kill(entireProcessTree: true);
        process.WaitForExit(milliseconds: 5_000);
    }

    private sealed record TrackedProcess(int ProcessId, DateTime StartTimeUtc);
}
