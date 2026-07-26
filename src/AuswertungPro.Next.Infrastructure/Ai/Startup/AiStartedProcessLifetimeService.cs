using System.Diagnostics;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Startup;

/// <summary>
/// Verfolgt ausschliesslich KI-Prozesse, die SewerStudio selbst gestartet hat.
/// Bereits vorher laufende Ollama- oder Sidecar-Prozesse werden nie registriert oder beendet.
/// Paket 2/A3: Jeder Eintrag traegt Startzeit, Prozessart und (optionale) Programmdatei;
/// veraltete Eintraege (Prozess beendet oder PID wiederverwendet) werden bei jeder
/// Abfrage entfernt, damit eine wiederverwendete PID nie als "eigener Prozess" gilt.
/// </summary>
public sealed class AiStartedProcessLifetimeService : IAiStartedProcessLifetime
{
    private readonly object _sync = new();
    private readonly List<TrackedProcess> _trackedProcesses = [];
    private WindowsKillOnCloseJob? _windowsJob;
    // Start-Berechtigung (nie Kill-Berechtigung): wurde je ein eigener Sidecar getrackt,
    // gilt er auch nach seinem Ende (z. B. Watchdog-Exit) als wiederstartbar (Paket 2/B2).
    private bool _hadSidecarProcess;

    public bool TryTrack(Process process, out string? error)
        => TryTrack(process, AiStartedProcessKind.Unknown, expectedImagePath: null, out error);

    public bool TryTrack(Process process, AiStartedProcessKind kind, string? expectedImagePath, out string? error)
    {
        ArgumentNullException.ThrowIfNull(process);

        lock (_sync)
        {
            var tracked = TryRememberProcess(process, kind, expectedImagePath);
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

    /// <summary>
    /// True, wenn mindestens ein selbst gestarteter KI-Prozess verfolgt wird (Paket 3/A2).
    /// Veraltete Eintraege werden vor der Antwort entfernt (Paket 2/A3).
    /// </summary>
    public bool HasTrackedStartedProcesses
    {
        get
        {
            lock (_sync)
            {
                PruneDeadProcesses();
                return _trackedProcesses.Count > 0;
            }
        }
    }

    /// <summary>
    /// True, wenn ein getrackter Prozess die Art Sidecar traegt (Paket 2/A3) — Grundlage
    /// gegen blinde Zweitstarts: nur Ollama reicht fuer einen Sidecar-Neustart nicht.
    /// </summary>
    public bool HasTrackedSidecarProcess
    {
        get
        {
            lock (_sync)
            {
                PruneDeadProcesses();
                return _trackedProcesses.Any(item => item.Kind == AiStartedProcessKind.Sidecar);
            }
        }
    }

    /// <summary>
    /// PIDs der aktuell lebenden getrackten Prozesse einer Art (Paket 2/B2) — Grundlage,
    /// um NIEMALS neben einem laufenden eigenen Sidecar zu starten: ein solcher Prozess
    /// wird vor einem Neustart zuerst verifiziert beendet.
    /// </summary>
    public IReadOnlyList<int> GetLiveTrackedProcessPids(AiStartedProcessKind kind)
    {
        lock (_sync)
        {
            PruneDeadProcesses();
            return _trackedProcesses
                .Where(item => item.Kind == kind)
                .Select(item => item.ProcessId)
                .ToArray();
        }
    }

    /// <summary>
    /// True, wenn in dieser Sitzung jemals ein eigener Sidecar-Prozess getrackt wurde —
    /// auch wenn er inzwischen beendet ist (z. B. Watchdog-Exit). Start-,
    /// NIEMALS Kill-Berechtigung (Paket 2/B2).
    /// </summary>
    public bool HadTrackedSidecarProcess
    {
        get
        {
            lock (_sync)
                return _hadSidecarProcess;
        }
    }

    /// <summary>
    /// True, wenn die Prozess-ID einem selbst gestarteten Prozess gehoert UND der laufende
    /// Prozess dieselbe Startzeit traegt (PID-Reuse-Schutz, Paket 2/A3). Ein beendeter oder
    /// wiederverwendeter Eintrag wird entfernt und gilt nicht mehr als eigener Prozess.
    /// </summary>
    public bool IsTrackedProcess(int processId)
    {
        lock (_sync)
        {
            var tracked = _trackedProcesses.FirstOrDefault(item => item.ProcessId == processId);
            if (tracked is null)
                return false;

            if (!IsLiveMatch(tracked))
            {
                _trackedProcesses.Remove(tracked);
                return false;
            }

            return true;
        }
    }

    /// <summary>Identitaetsdaten (Startzeit, Art, Programmpfad) eines getrackten Prozesses.</summary>
    public TrackedAiProcessInfo? GetTrackedProcessInfo(int processId)
    {
        lock (_sync)
        {
            var tracked = _trackedProcesses.FirstOrDefault(item => item.ProcessId == processId);
            return tracked is null
                ? null
                : new TrackedAiProcessInfo(tracked.ProcessId, tracked.StartTimeUtc, tracked.Kind, tracked.ExpectedImagePath);
        }
    }

    /// <summary>
    /// Beendet gezielt EINEN verfolgten Prozess samt Baum (z.B. den haengenden
    /// Sidecar-Wrapper beim kontrollierten Neustart). Fremdprozesse bleiben unangetastet.
    /// </summary>
    public void StopTrackedProcess(int processId)
    {
        TrackedProcess? tracked;
        lock (_sync)
        {
            tracked = _trackedProcesses.FirstOrDefault(item => item.ProcessId == processId);
            if (tracked is not null)
                _trackedProcesses.Remove(tracked);
        }

        if (tracked is null)
            return;

        BestEffort.Try(
            () => KillIfSameProcess(tracked),
            $"[KI-Prozesse] Prozess {processId} gezielt beenden");
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

    private bool TryRememberProcess(Process process, AiStartedProcessKind kind, string? expectedImagePath)
    {
        try
        {
            _trackedProcesses.RemoveAll(item => item.ProcessId == process.Id);
            _trackedProcesses.Add(new TrackedProcess(
                process.Id,
                process.StartTime.ToUniversalTime(),
                kind,
                string.IsNullOrWhiteSpace(expectedImagePath) ? null : expectedImagePath));
            if (kind == AiStartedProcessKind.Sidecar)
                _hadSidecarProcess = true;   // Start-Berechtigung auch nach dem Prozessende (B2)
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

    /// <summary>Entfernt beendete/wiederverwendete Eintraege. Aufrufer haelt _sync.</summary>
    private void PruneDeadProcesses()
        => _trackedProcesses.RemoveAll(item => !IsLiveMatch(item));

    /// <summary>
    /// Live-Identitaetspruefung: Der Prozess muss laufen und exakt die registrierte
    /// Startzeit tragen. Nicht lesbare Prozesse (weg, Zugriffsfehler) gelten konservativ
    /// als "nicht (mehr) getrackt" — nie raten, sonst koennte ein fremder Prozess mit
    /// wiederverwendeter PID beendet werden.
    /// </summary>
    private static bool IsLiveMatch(TrackedProcess tracked)
    {
        try
        {
            using var process = Process.GetProcessById(tracked.ProcessId);
            return !process.HasExited
                   && process.StartTime.ToUniversalTime() == tracked.StartTimeUtc;
        }
        catch
        {
            return false;
        }
    }

    private static void KillIfSameProcess(TrackedProcess tracked)
    {
        using var process = Process.GetProcessById(tracked.ProcessId);
        if (process.HasExited || process.StartTime.ToUniversalTime() != tracked.StartTimeUtc)
            return;

        if (!MatchesExpectedImage(process, tracked.ExpectedImagePath))
            return;

        process.Kill(entireProcessTree: true);
        process.WaitForExit(milliseconds: 5_000);
    }

    /// <summary>
    /// Programmdatei-Check (Paket 2/A3): Nur pruefbar, wenn ein erwarteter Pfad hinterlegt
    /// UND die tatsaechliche Datei lesbar ist — die Startzeit bleibt der primaere Anker.
    /// Verglichen wird der Dateiname (Pfade/Extension unterscheiden sich je nach Aufloesung).
    /// </summary>
    private static bool MatchesExpectedImage(Process process, string? expectedImagePath)
    {
        if (string.IsNullOrWhiteSpace(expectedImagePath))
            return true;

        string? actualPath;
        try
        {
            actualPath = process.MainModule?.FileName;
        }
        catch
        {
            return true;   // Zugriffsfehler: keine Aussage moeglich — Startzeit prueft die Identitaet.
        }

        if (string.IsNullOrWhiteSpace(actualPath))
            return true;

        return ProcessTreeInspector.ImageFileNameMatches(actualPath, expectedImagePath);
    }

    private sealed record TrackedProcess(
        int ProcessId,
        DateTime StartTimeUtc,
        AiStartedProcessKind Kind,
        string? ExpectedImagePath);
}
