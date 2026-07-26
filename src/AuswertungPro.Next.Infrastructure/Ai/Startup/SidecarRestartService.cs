using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Startup;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Ai.Startup;

/// <summary>
/// Kontrollierter Sidecar-Neustart nach einem Inferenz-Ausfall (Paket 3/A2):
/// stale Prozessbaum beenden, Sidecar ueber den bestehenden Startweg
/// (PowerShell-Skript + IAiStartupLauncher, Prozess wird getrackt) starten,
/// auf /health warten.
///
/// Sicherheitsregel: Ein Neustart wird NUR versucht, wenn die App den Sidecar selbst
/// gestartet hat — nachgewiesen ueber die getrackten Prozesse und die Vorfahrenkette
/// der /health-PID. Ein fremd gestarteter Sidecar wird nie beendet (graceful Degraded).
///
/// Paket 2/A3 (haerte Absicherung):
/// - Vor JEDEM Kill wird die Identitaet erneut geprueft (PID + Startzeit + Programmdatei,
///   defensiv bei Zugriffsfehlern): PID-Reuse oder fremde Programmdatei = KEIN Kill.
/// - KillProcessTree meldet den Erfolg; bei Fehler oder Timeout wird NICHT neu gestartet
///   (sonst liefen zwei Sidecars parallel / Portkonflikt).
/// - Kein blinder Zweitstart: ohne lesbare /health-PID ist ein Neustart nur erlaubt, wenn
///   ein eigener Prozess der Art Sidecar getrackt ist — nur Ollama reicht nicht.
/// - Ollama-Prozesse werden nie beendet (expliziter Guard, Kill-Ziele nur Sidecar-Baum).
/// - Erfolg erst bei stabilem Health: mindestens 2 aufeinanderfolgende /health-Polls.
/// </summary>
public sealed class SidecarRestartService : ISidecarRestartService
{
    private static readonly TimeSpan DefaultHealthTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan KillWaitTimeout = TimeSpan.FromSeconds(10);

    // Stabiles Health: erst N aufeinanderfolgende erfolgreiche /health-Polls gelten
    // als "wieder oben" (ein einzelner Poll kann einem sterbenden Prozess trügen).
    private const int RequiredConsecutiveHealthyPolls = 2;

    private readonly IAiStartedProcessLifetime _startedProcesses;
    private readonly IAiStartupLauncher _launcher;
    private readonly Func<SidecarRestartTarget> _getTarget;
    private readonly Func<int, IReadOnlyList<int>> _ancestorIds;
    private readonly Func<SidecarRestartTarget, CancellationToken, Task<int?>> _readProcessId;
    private readonly Func<int, ProcessIdentityProbe> _processProbe;
    private readonly Func<int, TimeSpan, bool> _killProcessTree;
    private readonly ILogger _logger;
    private readonly TimeSpan _healthTimeout;
    private readonly TimeSpan _pollInterval;

    private readonly object _sync = new();
    private bool _restartInFlight;

    public SidecarRestartService(
        IAiStartedProcessLifetime startedProcesses,
        IAiStartupLauncher launcher,
        Func<SidecarRestartTarget> getTarget,
        Func<int, IReadOnlyList<int>>? ancestorIds = null,
        Func<SidecarRestartTarget, CancellationToken, Task<int?>>? readProcessId = null,
        ILogger? logger = null,
        TimeSpan? healthTimeout = null,
        TimeSpan? pollInterval = null,
        Func<int, ProcessIdentityProbe>? processProbe = null,
        Func<int, TimeSpan, bool>? killProcessTree = null)
    {
        _startedProcesses = startedProcesses ?? throw new ArgumentNullException(nameof(startedProcesses));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _getTarget = getTarget ?? throw new ArgumentNullException(nameof(getTarget));
        _ancestorIds = ancestorIds ?? ProcessTreeInspector.GetAncestorIds;
        _readProcessId = readProcessId ?? ReadProcessIdViaHttpAsync;
        _processProbe = processProbe ?? ProcessTreeInspector.ProbeProcessIdentity;
        _killProcessTree = killProcessTree ?? ProcessTreeInspector.KillProcessTree;
        _logger = logger ?? NullLogger.Instance;
        _healthTimeout = healthTimeout ?? DefaultHealthTimeout;
        _pollInterval = pollInterval ?? DefaultPollInterval;
    }

    public async Task<SidecarRestartResult> TryRestartAsync(
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        // Nur ein Neustart gleichzeitig (mehrere Laeufe/Fenster teilen den Dienst).
        lock (_sync)
        {
            if (_restartInFlight)
                return new SidecarRestartResult(false, false, "Ein Sidecar-Neustart laeuft bereits.");
            _restartInFlight = true;
        }

        try
        {
            return await TryRestartCoreAsync(progress, ct).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
                _restartInFlight = false;
        }
    }

    private async Task<SidecarRestartResult> TryRestartCoreAsync(
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var target = _getTarget();

        // 1) Startrecht: ueberhaupt nur, wenn die App selbst KI-Prozesse gestartet hat
        // (lebendig getrackt) ODER in dieser Sitzung bereits einen eigenen Sidecar besass
        // (Paket 2/B2: nach einem Watchdog-Exit ist das Tracking gepruned — der eigene,
        // jetzt beendete Sidecar bleibt wiederstartbar; fremde Prozesse werden dafuer
        // niemals angefasst, Kill-Entscheidungen verlangen weiter die Live-Identitaet).
        if (!_startedProcesses.HasTrackedStartedProcesses
            && !_startedProcesses.HadTrackedSidecarProcess)
        {
            _logger.LogWarning(
                "Sidecar-Neustart abgelehnt: kein selbst gestarteter KI-Prozess bekannt (fremder Sidecar).");
            return new SidecarRestartResult(false, false,
                "Sidecar wurde nicht von SewerStudio gestartet – kein Neustart.");
        }

        // 2) Laufenden Sidecar-Prozess bestimmen (best effort; ein toter Prozess liefert keine PID).
        var sidecarPid = await _readProcessId(target, ct).ConfigureAwait(false);
        if (sidecarPid is int pid)
        {
            var ancestors = _ancestorIds(pid);
            var isOurs = IsTrackedSidecarProcess(pid)
                         || ancestors.Any(IsTrackedSidecarProcess);
            if (!isOurs)
            {
                _logger.LogWarning(
                    "Sidecar-Neustart abgelehnt: laufender Sidecar (PID {Pid}) gehoert nicht zu SewerStudio.", pid);
                return new SidecarRestartResult(false, false,
                    $"Laufender Sidecar (PID {pid}) wurde nicht von SewerStudio gestartet – kein Neustart.");
            }

            // 3) Stale Prozessbaum kontrolliert beenden — mit erneuter Identitaetspruefung
            // direkt vor dem Kill. Fehler/Timeout/Identitaetszweifel -> KEIN Neustart,
            // sonst liefen zwei Sidecars parallel (Portkonflikt).
            // Identitaets-Snapshot zum Zeitpunkt der Eigentumspruefung (Paket 2/B3):
            // Startzeit und Programmdatei des Python-Kindprozesses werden von diesem
            // Moment an bis unmittelbar vor den Kill gebunden.
            var ownershipProbe = _processProbe(pid);
            progress?.Report("Beende hängenden Sidecar-Prozessbaum...");
            if (!KillProcessTreeVerified(pid, ownershipProbe))
            {
                return new SidecarRestartResult(true, false,
                    $"Hängender Sidecar-Prozess (PID {pid}) konnte nicht sicher beendet werden – " +
                    "Neustart abgebrochen, damit kein zweiter Sidecar parallel läuft.");
            }

            // Getrackte Wrapper (PowerShell-Skript) beenden — Ollama-Prozesse nie anfassen.
            foreach (var ancestorPid in ancestors)
            {
                var info = _startedProcesses.GetTrackedProcessInfo(ancestorPid);
                if (info?.Kind != AiStartedProcessKind.Sidecar)
                    continue;

                _startedProcesses.StopTrackedProcess(ancestorPid);
            }
        }
        else
        {
            // Keine /health-PID lesbar (Paket 2/B2): NIEMALS neben einem laufenden
            // Sidecar starten. Ein noch lebender eigener Sidecar-Prozess wird zuerst
            // verifiziert beendet (dieselbe Identitaetspruefung wie im PID-Fall); ein
            // frueher eigener, bereits beendeter Sidecar (z. B. nach Watchdog-Exit)
            // bleibt startberechtigt — ohne dass dafuer ein fremder Prozess angefasst
            // wuerde. Nur Ollama/Unbekanntes getrackt: kein Blindstart.
            var liveSidecarPids = _startedProcesses.GetLiveTrackedProcessPids(AiStartedProcessKind.Sidecar);
            if (liveSidecarPids.Count > 0)
            {
                foreach (var ownPid in liveSidecarPids)
                {
                    progress?.Report("Health nicht lesbar – beende hängenden eigenen Sidecar-Prozess...");
                    if (!KillProcessTreeVerified(ownPid))
                    {
                        return new SidecarRestartResult(true, false,
                            $"Eigener Sidecar-Prozess (PID {ownPid}) konnte nicht sicher beendet werden – " +
                            "Neustart abgebrochen, damit kein zweiter Sidecar parallel läuft.");
                    }
                }
            }
            else if (!_startedProcesses.HadTrackedSidecarProcess)
            {
                _logger.LogWarning(
                    "Sidecar-Neustart abgelehnt: keine Sidecar-PID lesbar und nie ein eigener " +
                    "Sidecar-Prozess gestartet (kein Blindstart).");
                return new SidecarRestartResult(false, false,
                    "Sidecar-PID nicht lesbar und kein selbst gestarteter Sidecar-Prozess bekannt – " +
                    "kein Neustart (kein Blindstart).");
            }
            // HadTrackedSidecarProcess ohne lebenden Prozess: der eigene Sidecar ist
            // beendet (auch Watchdog-Exit) — der Wiederstart ist erlaubt, nichts zu beenden.
        }

        // 4) Neu starten ueber den bestehenden Startweg (Skript + Launcher, Prozess wird getrackt).
        if (string.IsNullOrWhiteSpace(target.ScriptPath) || !File.Exists(target.ScriptPath))
        {
            return new SidecarRestartResult(true, false,
                $"Sidecar-Startskript nicht gefunden: {target.ScriptPath ?? "(leer)"}");
        }

        progress?.Report("Starte Vision-Sidecar neu...");
        var started = _launcher.TryStart(
            new AiStartupProcessRequest(
                FileName: target.PowerShellExe,
                Arguments: $"-NoProfile -ExecutionPolicy Bypass -File \"{target.ScriptPath}\"",
                WorkingDirectory: Path.GetDirectoryName(target.ScriptPath),
                Hidden: true)
            {
                EnvironmentVariables = target.EnvironmentVariables
            },
            out var startError);
        if (!started)
            return new SidecarRestartResult(true, false, $"Sidecar-Start fehlgeschlagen: {startError}");

        // 5) Auf /health warten (Kaltstart inkl. TensorRT kann 1-2 Min dauern) — Erfolg erst
        // bei stabilem Health: mind. 2 aufeinanderfolgende erfolgreiche Polls (Paket 2/A3).
        var deadline = DateTime.UtcNow + _healthTimeout;
        var consecutiveHealthyPolls = 0;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (await _launcher.IsReachableAsync(target.SidecarUrl, "/health", target.Headers, ct).ConfigureAwait(false))
            {
                consecutiveHealthyPolls++;
                if (consecutiveHealthyPolls < RequiredConsecutiveHealthyPolls)
                {
                    await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
                    continue;
                }

                // Best effort: Modelle wieder warm laden (wie "KI starten"); ein Fehler hier
                // ist nicht fatal — die Modelle laden sonst beim ersten Frame nach.
                progress?.Report("Sidecar wieder erreichbar – lade Modelle...");
                var warm = await _launcher.WarmupSidecarModelsAsync(target.SidecarUrl, target.Headers, ct).ConfigureAwait(false);
                _logger.LogInformation(
                    "Sidecar-Neustart erfolgreich nach {Polls} stabilen Health-Polls (Warmup: {Warmup}).",
                    consecutiveHealthyPolls,
                    warm.Succeeded ? "ok" : warm.Error ?? "fehlgeschlagen");
                return new SidecarRestartResult(true, true, null);
            }

            consecutiveHealthyPolls = 0;
            await Task.Delay(_pollInterval, ct).ConfigureAwait(false);
        }

        return new SidecarRestartResult(true, false,
            $"Sidecar nach Neustart nicht erreichbar (Timeout {(int)_healthTimeout.TotalSeconds}s).");
    }

    private bool IsTrackedSidecarProcess(int processId)
        => _startedProcesses.GetTrackedProcessInfo(processId)?.Kind == AiStartedProcessKind.Sidecar;

    /// <summary>
    /// Beendet den Sidecar-Prozessbaum — aber nur nach erneuter Identitaetspruefung
    /// (Paket 2/A3, TOCTOU-Schutz): Ein getrackter Prozess muss Startzeit (und, sofern
    /// hinterlegt und lesbar, die Programmdatei) bestaetigen; ein Ollama-Prozess wird
    /// grundsaetzlich nie beendet. Paket 2/B3: Ein Python-Kindprozess OHNE eigenen
    /// Tracking-Eintrag (Besitz nur ueber einen getrackten Vorfahren bewiesen) muss eine
    /// Python-Programmdatei tragen und wird per <paramref name="baseline"/>-Snapshot der
    /// Eigentumspruefung gebunden; unmittelbar vor dem Kill wird die Identitaet ein
    /// letztes Mal geprueft. true = Baum beendet (oder Prozess lief schon nicht mehr);
    /// false = abgelehnt/fehlgeschlagen/Timeout -> KEIN Neustart.
    /// </summary>
    private bool KillProcessTreeVerified(int processId, ProcessIdentityProbe? baseline = null)
    {
        var probeBefore = _processProbe(processId);
        if (!probeBefore.Found)
            return true;   // Prozess ist bereits weg — das Kill-Ziel ist erreicht.

        // Baseline-Bindung (B3): die Eigentumspruefung geschah zum Baseline-Zeitpunkt —
        // jede Abweichung danach bedeutet PID-Wiederverwendung -> kein Kill.
        if (baseline is { } bound
            && (bound.StartTimeUtc != probeBefore.StartTimeUtc
                || !string.Equals(bound.ImagePath, probeBefore.ImagePath, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning(
                "Kill abgelehnt: Identitaet von PID {Pid} weicht von der Eigentumspruefung ab (PID-Reuse?).",
                processId);
            return false;
        }

        var tracked = _startedProcesses.GetTrackedProcessInfo(processId);
        if (tracked is not null)
        {
            // Nur ein ausdruecklich als Sidecar klassifizierter Prozess darf ein
            // Kill-Ziel sein. Unknown und Ollama bleiben fail-closed unangetastet.
            if (tracked.Kind != AiStartedProcessKind.Sidecar)
            {
                _logger.LogWarning(
                    "Kill abgelehnt: PID {Pid} ist kein ausdruecklich getrackter Sidecar-Prozess ({Kind}).",
                    processId, tracked.Kind);
                return false;
            }

            // PID-Reuse-Schutz: ohne Startzeit-Uebereinstimmung ist die Identitaet nicht bewiesen.
            if (probeBefore.StartTimeUtc is null || probeBefore.StartTimeUtc.Value != tracked.StartTimeUtc)
            {
                _logger.LogWarning(
                    "Kill abgelehnt: Startzeit von PID {Pid} passt nicht zum getrackten Prozess (PID-Reuse?).",
                    processId);
                return false;
            }

            // Ist ein erwarteter Programmpfad hinterlegt, muss auch die aktuelle
            // Programmdatei lesbar und passend sein. Unlesbar bleibt fail-closed.
            if (!string.IsNullOrWhiteSpace(tracked.ExpectedImagePath)
                && (string.IsNullOrWhiteSpace(probeBefore.ImagePath)
                    || !ProcessTreeInspector.ImageFileNameMatches(
                        probeBefore.ImagePath,
                        tracked.ExpectedImagePath)))
            {
                _logger.LogWarning(
                    "Kill abgelehnt: Programmdatei von PID {Pid} ({Actual}) passt nicht zum getrackten Prozess ({Expected}).",
                    processId, probeBefore.ImagePath ?? "(unlesbar)", tracked.ExpectedImagePath);
                return false;
            }
        }
        else if (!ProcessTreeInspector.IsPythonInterpreterImage(probeBefore.ImagePath))
        {
            // Python-Kindprozess ohne eigenen Tracking-Eintrag (Besitz nur ueber einen
            // getrackten Vorfahren bewiesen, Paket 2/B3): die Programmdatei muss ein
            // Python-Interpreter sein — unlesbar oder fremd = kein Kill.
            _logger.LogWarning(
                "Kill abgelehnt: PID {Pid} ist kein identitaetsgebundener Python-Prozess (Programmdatei: {Image}).",
                processId, probeBefore.ImagePath ?? "(unlesbar)");
            return false;
        }

        // TOCTOU-Deckel: unmittelbar vor dem Kill ein letztes Mal die Identitaet pruefen.
        var probeFinal = _processProbe(processId);
        if (!probeFinal.Found)
            return true;   // zwischenzeitlich von selbst beendet — Ziel erreicht.
        if (probeFinal.StartTimeUtc != probeBefore.StartTimeUtc
            || !string.Equals(probeFinal.ImagePath, probeBefore.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Kill abgelehnt: Identitaet von PID {Pid} hat sich kurz vor dem Kill geaendert (PID-Reuse?).",
                processId);
            return false;
        }

        _logger.LogWarning("Beende haengenden Sidecar-Prozessbaum (PID {Pid}).", processId);
        if (!_killProcessTree(processId, KillWaitTimeout))
        {
            _logger.LogWarning(
                "Sidecar-Prozessbaum (PID {Pid}) konnte nicht beendet werden (Fehler oder Timeout) — kein Neustart.",
                processId);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Liest die Sidecar-PID aus /health (kurzes 5s-Cap). Ein haengender CUDA-Call blockiert
    /// den FastAPI-Event-Loop nicht (Predict laeuft im Threadpool), deshalb antwortet /health
    /// meist noch; ein toter Prozess liefert keine PID (dann ist nichts zu beenden).
    /// </summary>
    private static async Task<int?> ReadProcessIdViaHttpAsync(SidecarRestartTarget target, CancellationToken ct)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            using var http = new HttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(target.SidecarUrl, "/health"));
            if (target.Headers is not null)
            {
                foreach (var pair in target.Headers)
                    req.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
            }

            using var resp = await http.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            var body = await resp.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("process_id", out var pidElement)
                   && pidElement.TryGetInt32(out var pid)
                ? pid
                : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
