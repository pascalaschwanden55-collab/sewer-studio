using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Startup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Startup;

/// <summary>
/// Tests fuer den kontrollierten Sidecar-Neustartdienst (Paket 3/A2, gehaertet in Paket 2/A3).
/// Prozess-/HTTP-Zugriffe laufen ueber injizierbare Seams (readProcessId/ancestorIds/
/// processProbe/killProcessTree/Launcher/Lifetime) — es werden keine echten Sidecar-Prozesse
/// gestartet und keine echten Prozesse beendet.
/// </summary>
public sealed class SidecarRestartServiceTests : IDisposable
{
    // Plausibel unmoegliche PID (Windows-PIDs sind klein und durch 4 teilbar):
    // GetProcessById schlaegt damit garantiert fehl -> Kill/Probe bleibt im Test ein No-op.
    private const int FakeSidecarPid = 1_073_741_820;

    private static readonly DateTime TrackedStart = new(2026, 7, 26, 10, 0, 0, DateTimeKind.Utc);

    private readonly string _scriptPath;

    public SidecarRestartServiceTests()
    {
        _scriptPath = Path.Combine(Path.GetTempPath(), "sewerstudio_restart_test_" + Guid.NewGuid().ToString("N") + ".ps1");
        File.WriteAllText(_scriptPath, "# Testplatzhalter\n");
    }

    public void Dispose()
    {
        try { File.Delete(_scriptPath); } catch { /* best effort */ }
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeLifetime : IAiStartedProcessLifetime
    {
        public HashSet<int> Tracked { get; } = new();
        public HashSet<int> SidecarPids { get; } = new();
        public HashSet<int> OllamaPids { get; } = new();
        // Paket 2/B2: lebende eigene Sidecar-Prozesse (GetLiveTrackedProcessPids)
        // bzw. Start-Berechtigung nach beendetem eigenem Sidecar (HadTrackedSidecarProcess).
        public HashSet<int> LiveSidecarPids { get; } = new();
        public bool HadSidecar { get; set; }
        public Dictionary<int, DateTime> StartTimes { get; } = new();
        public Dictionary<int, string> ImagePaths { get; } = new();
        public List<int> StoppedIndividually { get; } = new();

        public bool TryTrack(Process process, out string? error)
        {
            Tracked.Add(process.Id);
            error = null;
            return true;
        }

        public void StopAllStartedProcesses() => Tracked.Clear();

        public bool HasTrackedStartedProcesses => Tracked.Count > 0;

        public bool HasTrackedSidecarProcess => SidecarPids.Count > 0;

        public bool IsTrackedProcess(int processId) => Tracked.Contains(processId);

        public TrackedAiProcessInfo? GetTrackedProcessInfo(int processId)
            => Tracked.Contains(processId)
                ? new TrackedAiProcessInfo(
                    processId,
                    StartTimes.TryGetValue(processId, out var start) ? start : default,
                    OllamaPids.Contains(processId) ? AiStartedProcessKind.Ollama
                        : SidecarPids.Contains(processId) ? AiStartedProcessKind.Sidecar
                        : AiStartedProcessKind.Unknown,
                    ImagePaths.TryGetValue(processId, out var path) ? path : null)
                : null;

        public void StopTrackedProcess(int processId)
        {
            Tracked.Remove(processId);
            StoppedIndividually.Add(processId);
        }

        public IReadOnlyList<int> GetLiveTrackedProcessPids(AiStartedProcessKind kind)
            => kind == AiStartedProcessKind.Sidecar
                ? LiveSidecarPids.ToArray()
                : Array.Empty<int>();

        public bool HadTrackedSidecarProcess
            => HadSidecar || SidecarPids.Count > 0 || LiveSidecarPids.Count > 0;
    }

    private sealed class FakeLauncher : IAiStartupLauncher
    {
        public int ReachableAfterPolls = 1;   // ab welchem Poll /health ok meldet (int.MaxValue = nie)

        /// <summary>
        /// Optionale Antwortsequenz fuer /health (Flatter-Tests): das letzte Element wird
        /// wiederholt, sobald die Sequenz verbraucht ist. Uebersteuert ReachableAfterPolls.
        /// </summary>
        public Queue<bool>? ReachableSequence;
        public bool StartResult = true;
        public string? StartError;
        public int TryStartCalls;
        public int ReachableCalls;
        public int WarmupCalls;
        public AiStartupProcessRequest? LastStartRequest;

        public Task<bool> IsReachableAsync(
            Uri baseUri, string relativePath, IReadOnlyDictionary<string, string>? headers, CancellationToken ct)
        {
            ReachableCalls++;
            if (ReachableSequence is { Count: > 0 } sequence)
            {
                // Letztes Element haelt den Dauerzustand (z. B. dauerhaft true/false).
                var next = sequence.Count > 1 ? sequence.Dequeue() : sequence.Peek();
                return Task.FromResult(next);
            }

            return Task.FromResult(ReachableCalls >= ReachableAfterPolls);
        }

        public bool TryStart(AiStartupProcessRequest request, out string? error)
        {
            TryStartCalls++;
            LastStartRequest = request;
            error = StartError;
            return StartResult;
        }

        public Task<AiStartupModelPreloadResult> PreloadOllamaModelAsync(
            Uri baseUri, AiStartupModelPreloadRequest request, CancellationToken ct)
            => Task.FromResult(new AiStartupModelPreloadResult(true, null));

        public Task<bool?> IsOllamaModelResidentAsync(Uri baseUri, string modelName, CancellationToken ct)
            => Task.FromResult<bool?>(true);

        public Task<AiStartupWarmupResult> WarmupSidecarModelsAsync(
            Uri sidecarBaseUri, IReadOnlyDictionary<string, string>? headers, CancellationToken ct)
        {
            WarmupCalls++;
            return Task.FromResult(new AiStartupWarmupResult(true, new[] { "yolo", "dino", "sam" }, null));
        }
    }

    /// <summary>Zeichnet die Kill-Aufrufe auf; Ergebnis frei waehlbar.</summary>
    private sealed class FakeKill
    {
        public List<int> Calls { get; } = new();
        public bool Result = true;

        public bool Kill(int pid, TimeSpan timeout)
        {
            Calls.Add(pid);
            return Result;
        }
    }

    private SidecarRestartService CreateService(
        FakeLifetime lifetime,
        FakeLauncher launcher,
        int? healthPid,
        IReadOnlyList<int>? ancestors = null,
        TimeSpan? healthTimeout = null,
        TimeSpan? pollInterval = null,
        Func<int, ProcessIdentityProbe>? processProbe = null,
        Func<int, TimeSpan, bool>? killProcessTree = null)
        => new(
            lifetime,
            launcher,
            getTarget: () => new SidecarRestartTarget(
                SidecarUrl: new Uri("http://127.0.0.1:8100"),
                Headers: null,
                ScriptPath: _scriptPath,
                PowerShellExe: "powershell",
                EnvironmentVariables: null),
            ancestorIds: _ => ancestors ?? Array.Empty<int>(),
            readProcessId: (_, _) => Task.FromResult(healthPid),
            healthTimeout: healthTimeout ?? TimeSpan.FromSeconds(5),
            pollInterval: pollInterval ?? TimeSpan.FromMilliseconds(10),
            processProbe: processProbe,
            killProcessTree: killProcessTree);

    // ── Tests: Besitznachweis / Ablehnung ────────────────────────────────────

    [Fact]
    public async Task Kein_eigener_prozess_kein_neustart()
    {
        var lifetime = new FakeLifetime();   // nichts getrackt
        var launcher = new FakeLauncher();
        var svc = CreateService(lifetime, launcher, healthPid: FakeSidecarPid, ancestors: new[] { 555 });

        var result = await svc.TryRestartAsync();

        Assert.False(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Equal(0, launcher.TryStartCalls);
        Assert.Empty(lifetime.StoppedIndividually);
    }

    [Fact]
    public async Task Fremder_sidecar_kein_neustart_graceful_degraded()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(999);                       // App hat IRGENDEINEN KI-Prozess gestartet
        var launcher = new FakeLauncher();
        // Weder die Sidecar-PID noch ihre Vorfahren sind getrackt -> fremd.
        var svc = CreateService(lifetime, launcher, healthPid: FakeSidecarPid, ancestors: new[] { 555, 556 });

        var result = await svc.TryRestartAsync();

        Assert.False(result.Attempted);
        Assert.Contains("nicht von SewerStudio gestartet", result.Reason);
        Assert.Equal(0, launcher.TryStartCalls);
        Assert.Empty(lifetime.StoppedIndividually);
    }

    [Fact]
    public async Task Eigener_sidecar_neustart_killt_startet_und_wartet_auf_health()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);                       // Wrapper-PID, Vorfahre des Sidecars
        lifetime.SidecarPids.Add(555);
        var launcher = new FakeLauncher { ReachableAfterPolls = 2 };
        var svc = CreateService(lifetime, launcher, healthPid: FakeSidecarPid, ancestors: new[] { 555 });

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(1, launcher.TryStartCalls);
        Assert.Equal(1, launcher.WarmupCalls);
        Assert.Contains(555, lifetime.StoppedIndividually);
        Assert.NotNull(launcher.LastStartRequest);
        Assert.Contains(_scriptPath, launcher.LastStartRequest!.Arguments);
    }

    [Fact]
    public async Task Toter_sidecar_ohne_health_pid_startet_direkt_neu()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.SidecarPids.Add(555);   // Paket 2/A3: Blindstart nur mit getracktem SIDECAR-Prozess
        var launcher = new FakeLauncher();
        // Kein PID-Leseergebnis (Prozess schon tot): nichts zu killen, direkt starten.
        var svc = CreateService(lifetime, launcher, healthPid: null);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(1, launcher.TryStartCalls);
        Assert.Empty(lifetime.StoppedIndividually);
    }

    [Fact]
    public async Task Nur_ollama_getrackt_kein_blindstart_und_ollama_bleibt_unangetastet()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(999);
        lifetime.OllamaPids.Add(999);    // NUR Ollama getrackt — kein eigener Sidecar
        var launcher = new FakeLauncher();
        var svc = CreateService(lifetime, launcher, healthPid: null);

        var result = await svc.TryRestartAsync();

        Assert.False(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Contains("kein Neustart", result.Reason);
        Assert.Equal(0, launcher.TryStartCalls);
        Assert.Empty(lifetime.StoppedIndividually);   // Ollama wird nie beendet
    }

    [Fact]
    public async Task Start_fehlgeschlagen_meldet_misserfolg()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.SidecarPids.Add(555);
        var launcher = new FakeLauncher { StartResult = false, StartError = "Skript nicht gefunden (Test)." };
        var svc = CreateService(lifetime, launcher, healthPid: null);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Contains("Skript nicht gefunden", result.Reason);
    }

    [Fact]
    public async Task Health_timeout_nach_neustart_meldet_misserfolg()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.SidecarPids.Add(555);
        var launcher = new FakeLauncher { ReachableAfterPolls = int.MaxValue };
        var svc = CreateService(
            lifetime, launcher, healthPid: null,
            healthTimeout: TimeSpan.FromMilliseconds(150),
            pollInterval: TimeSpan.FromMilliseconds(10));

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Contains("nicht erreichbar", result.Reason);
        Assert.Equal(1, launcher.TryStartCalls);
        Assert.Equal(0, launcher.WarmupCalls);
    }

    [Fact]
    public async Task Parallelaufruf_waehrend_neustart_wird_abgelehnt()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.SidecarPids.Add(555);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var launcher = new BlockingLauncher(gate.Task);
        var svc = new SidecarRestartService(
            lifetime,
            launcher,
            getTarget: () => new SidecarRestartTarget(
                new Uri("http://127.0.0.1:8100"), null, _scriptPath, "powershell", null),
            ancestorIds: _ => Array.Empty<int>(),
            readProcessId: (_, _) => Task.FromResult<int?>(null),
            healthTimeout: TimeSpan.FromSeconds(10),
            pollInterval: TimeSpan.FromMilliseconds(10));

        var first = svc.TryRestartAsync();
        // Warten, bis der erste Versuch im Health-Warten haengt.
        await launcher.ReachedHealthWait.WaitAsync(TimeSpan.FromSeconds(5));

        var second = await svc.TryRestartAsync();

        Assert.False(second.Attempted);
        Assert.Contains("bereits", second.Reason);

        gate.SetResult(true);
        var firstResult = await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(firstResult.Succeeded, firstResult.Reason);
    }

    // ── Paket 2/A3: Kill-Härtung ─────────────────────────────────────────────

    [Fact]
    public async Task Eigener_sidecar_genau_ein_kill_und_ein_start()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.SidecarPids.Add(555);
        var launcher = new FakeLauncher();
        var kill = new FakeKill();
        // Der haengende Python-Prozess existiert (Identitaet ohne Tracking-Bezug).
        var svc = CreateService(
            lifetime, launcher, healthPid: FakeSidecarPid, ancestors: new[] { 555 },
            processProbe: _ => new ProcessIdentityProbe(true, TrackedStart, "python.exe"),
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(new[] { FakeSidecarPid }, kill.Calls);   // genau 1 Kill am Python-Baum
        Assert.Equal(1, launcher.TryStartCalls);              // genau 1 Start
        Assert.Contains(555, lifetime.StoppedIndividually);   // Wrapper gezielt beendet
    }

    [Fact]
    public async Task Pid_reuse_andere_startzeit_kein_kill_kein_neustart()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(FakeSidecarPid);          // Sidecar-PID selbst getrackt...
        lifetime.SidecarPids.Add(FakeSidecarPid);
        lifetime.StartTimes[FakeSidecarPid] = TrackedStart;
        var launcher = new FakeLauncher();
        var kill = new FakeKill();
        // ...aber der laufende Prozess traegt eine ANDERE Startzeit (PID wiederverwendet).
        var svc = CreateService(
            lifetime, launcher, healthPid: FakeSidecarPid, ancestors: Array.Empty<int>(),
            processProbe: _ => new ProcessIdentityProbe(true, TrackedStart.AddHours(1), "python.exe"),
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Contains("nicht sicher beendet", result.Reason);
        Assert.Empty(kill.Calls);              // KEIN Kill bei Identitaetszweifel
        Assert.Equal(0, launcher.TryStartCalls);
    }

    [Fact]
    public async Task Falsche_programmdatei_kein_kill_kein_neustart()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(FakeSidecarPid);
        lifetime.SidecarPids.Add(FakeSidecarPid);
        lifetime.StartTimes[FakeSidecarPid] = TrackedStart;
        lifetime.ImagePaths[FakeSidecarPid] = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
        var launcher = new FakeLauncher();
        var kill = new FakeKill();
        // Gleiche Startzeit, aber eine fremde Programmdatei lauert unter der PID.
        var svc = CreateService(
            lifetime, launcher, healthPid: FakeSidecarPid, ancestors: Array.Empty<int>(),
            processProbe: _ => new ProcessIdentityProbe(true, TrackedStart, @"C:\Temp\fremd.exe"),
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Empty(kill.Calls);
        Assert.Equal(0, launcher.TryStartCalls);
    }

    [Fact]
    public async Task Unbekannte_prozessart_beweist_keinen_sidecar_besitz()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(FakeSidecarPid);
        lifetime.StartTimes[FakeSidecarPid] = TrackedStart;
        var launcher = new FakeLauncher();
        var kill = new FakeKill();
        var svc = CreateService(
            lifetime, launcher, healthPid: FakeSidecarPid, ancestors: Array.Empty<int>(),
            processProbe: _ => new ProcessIdentityProbe(true, TrackedStart, "python.exe"),
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.False(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Empty(kill.Calls);
        Assert.Equal(0, launcher.TryStartCalls);
    }

    [Fact]
    public async Task Erwartete_programmdatei_unlesbar_kein_kill_kein_neustart()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(FakeSidecarPid);
        lifetime.SidecarPids.Add(FakeSidecarPid);
        lifetime.StartTimes[FakeSidecarPid] = TrackedStart;
        lifetime.ImagePaths[FakeSidecarPid] =
            @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
        var launcher = new FakeLauncher();
        var kill = new FakeKill();
        var svc = CreateService(
            lifetime, launcher, healthPid: FakeSidecarPid, ancestors: Array.Empty<int>(),
            processProbe: _ => new ProcessIdentityProbe(true, TrackedStart, null),
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Empty(kill.Calls);
        Assert.Equal(0, launcher.TryStartCalls);
    }

    [Fact]
    public async Task Kill_fehlgeschlagen_kein_neustart_kein_zweiter_sidecar()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.SidecarPids.Add(555);
        var launcher = new FakeLauncher();
        var kill = new FakeKill { Result = false };   // Kill wirft oder Prozess endet nicht (Timeout)
        var svc = CreateService(
            lifetime, launcher, healthPid: FakeSidecarPid, ancestors: new[] { 555 },
            processProbe: _ => new ProcessIdentityProbe(true, TrackedStart, "python.exe"),
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Contains("nicht sicher beendet", result.Reason);
        Assert.Equal(new[] { FakeSidecarPid }, kill.Calls);
        Assert.Equal(0, launcher.TryStartCalls);       // NIE starten, wenn der alte Baum ggf. lebt
    }

    // ── Paket 2/B2: Kein Zweitstart ohne lesbare /health-PID ──────────────────

    [Fact]
    public async Task Keine_health_pid_lebender_eigener_sidecar_wird_zuerst_verifiziert_beendet()
    {
        // Sidecar-Prozess lebt noch, /health liefert keine PID: NIEMALS daneben starten —
        // der eigene Prozess wird zuerst identitaetsverifiziert beendet (B2).
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(777);
        lifetime.SidecarPids.Add(777);
        lifetime.LiveSidecarPids.Add(777);
        lifetime.StartTimes[777] = TrackedStart;
        lifetime.ImagePaths[777] = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
        var launcher = new FakeLauncher();
        var kill = new FakeKill();
        var svc = CreateService(
            lifetime, launcher, healthPid: null,
            processProbe: _ => new ProcessIdentityProbe(true, TrackedStart, @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"),
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(new[] { 777 }, kill.Calls);   // eigener Sidecar wurde ZUERST beendet
        Assert.Equal(1, launcher.TryStartCalls);   // erst danach gestartet
    }

    [Fact]
    public async Task Keine_health_pid_eigener_sidecar_nicht_beendbar_kein_start()
    {
        // Der lebende eigene Sidecar kann nicht sicher beendet werden -> KEIN Start,
        // sonst liefen zwei Sidecars parallel (B2).
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(777);
        lifetime.SidecarPids.Add(777);
        lifetime.LiveSidecarPids.Add(777);
        lifetime.StartTimes[777] = TrackedStart;
        lifetime.ImagePaths[777] = "powershell.exe";
        var launcher = new FakeLauncher();
        var kill = new FakeKill { Result = false };
        var svc = CreateService(
            lifetime, launcher, healthPid: null,
            processProbe: _ => new ProcessIdentityProbe(true, TrackedStart, "powershell.exe"),
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Equal(0, launcher.TryStartCalls);
    }

    [Fact]
    public async Task Keine_health_pid_beendeter_eigener_sidecar_watchdog_fall_startet_neu()
    {
        // Watchdog-Fall: der eigene Sidecar wurde bereits beendet (kein lebender Prozess
        // mehr getrackt), aber er gehoerte uns -> Wiederstart OHNE Kill erlaubt (B2/B3:
        // HadTrackedSidecarProcess ist Start-, keine Kill-Berechtigung).
        var lifetime = new FakeLifetime { HadSidecar = true };
        var launcher = new FakeLauncher();
        var kill = new FakeKill();
        var svc = CreateService(lifetime, launcher, healthPid: null, killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Succeeded, result.Reason);
        Assert.Empty(kill.Calls);                  // nichts zu killen — kein fremder Prozess angefasst
        Assert.Equal(1, launcher.TryStartCalls);
    }

    // ── Paket 2/B3: Identitaetsbindung des Python-Kindprozesses ───────────────

    [Fact]
    public async Task Python_kind_mit_fremder_programmdatei_kein_kill_kein_start()
    {
        // Besitz nur ueber Vorfahre bewiesen, aber die PID traegt KEIN Python-Image:
        // Identitaet unbewiesen -> kein Kill, kein Neustart (B3).
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.SidecarPids.Add(555);
        var launcher = new FakeLauncher();
        var kill = new FakeKill();
        var svc = CreateService(
            lifetime, launcher, healthPid: FakeSidecarPid, ancestors: new[] { 555 },
            processProbe: _ => new ProcessIdentityProbe(true, TrackedStart, @"C:\Temp\fremd.exe"),
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Empty(kill.Calls);
        Assert.Equal(0, launcher.TryStartCalls);
    }

    [Fact]
    public async Task Python_kind_mit_unlesbarer_programmdatei_kein_kill_kein_start()
    {
        // Programmdatei nicht lesbar = Identitaet nicht beweisbar (B3, konservativ).
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.SidecarPids.Add(555);
        var launcher = new FakeLauncher();
        var kill = new FakeKill();
        var svc = CreateService(
            lifetime, launcher, healthPid: FakeSidecarPid, ancestors: new[] { 555 },
            processProbe: _ => new ProcessIdentityProbe(true, TrackedStart, null),
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Empty(kill.Calls);
        Assert.Equal(0, launcher.TryStartCalls);
    }

    [Fact]
    public async Task Python_kind_identitaetswechsel_seit_eigentumspruefung_kein_kill()
    {
        // Baseline (Eigentumspruefung) und spaetere Probes weichen ab: die PID wurde
        // zwischen Besitznachweis und Kill wiederverwendet -> kein Kill (B3).
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.SidecarPids.Add(555);
        var launcher = new FakeLauncher();
        var kill = new FakeKill();
        var startAlt = DateTime.UtcNow.AddMinutes(-30);
        var startNeu = DateTime.UtcNow.AddMinutes(-5);
        var probeCalls = 0;
        var svc = CreateService(
            lifetime, launcher, healthPid: FakeSidecarPid, ancestors: new[] { 555 },
            processProbe: _ =>
            {
                probeCalls++;
                // Erste Probe = Eigentumspruefungs-Snapshot; danach "neuer" Prozess.
                return probeCalls == 1
                    ? new ProcessIdentityProbe(true, startAlt, "python.exe")
                    : new ProcessIdentityProbe(true, startNeu, "python.exe");
            },
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);
        Assert.Empty(kill.Calls);
        Assert.Equal(0, launcher.TryStartCalls);
    }

    [Fact]
    public async Task Ollama_vorfahre_wird_beim_neustart_nie_beendet()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.Tracked.Add(556);
        lifetime.SidecarPids.Add(555);
        lifetime.OllamaPids.Add(556);                  // Ollama haengt (hypothetisch) in der Kette
        var launcher = new FakeLauncher();
        var kill = new FakeKill();
        var svc = CreateService(
            lifetime, launcher, healthPid: FakeSidecarPid, ancestors: new[] { 555, 556 },
            processProbe: _ => new ProcessIdentityProbe(false, null, null),
            killProcessTree: kill.Kill);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Succeeded, result.Reason);
        Assert.Contains(555, lifetime.StoppedIndividually);
        Assert.DoesNotContain(556, lifetime.StoppedIndividually);   // Ollama unangetastet
    }

    [Fact]
    public async Task Health_flatterhaft_erst_nach_zwei_stabilen_polls_erfolg()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.SidecarPids.Add(555);
        var launcher = new FakeLauncher
        {
            ReachableSequence = new Queue<bool>(new[] { true, false, true, true })
        };
        var svc = CreateService(lifetime, launcher, healthPid: null);

        var result = await svc.TryRestartAsync();

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(4, launcher.ReachableCalls);   // true,false,true,true -> 2 stabile am Ende
    }

    [Fact]
    public async Task Health_dauerhaft_flatterhaft_kein_erfolg()
    {
        var lifetime = new FakeLifetime();
        lifetime.Tracked.Add(555);
        lifetime.SidecarPids.Add(555);
        var launcher = new FakeLauncher
        {
            ReachableSequence = new Queue<bool>(new[] { true, false })
        };
        var svc = CreateService(
            lifetime, launcher, healthPid: null,
            healthTimeout: TimeSpan.FromMilliseconds(300),
            pollInterval: TimeSpan.FromMilliseconds(10));

        var result = await svc.TryRestartAsync();

        Assert.True(result.Attempted);
        Assert.False(result.Succeeded);   // nie 2 aufeinanderfolgende Polls -> kein "stabil"
        Assert.Equal(0, launcher.WarmupCalls);
    }

    /// <summary>Launcher, der im Health-Poll blockiert, bis das Gate oeffnet.</summary>
    private sealed class BlockingLauncher : IAiStartupLauncher
    {
        private readonly Task _gate;
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingLauncher(Task gate) => _gate = gate;

        public Task ReachedHealthWait => _reached.Task;

        public async Task<bool> IsReachableAsync(
            Uri baseUri, string relativePath, IReadOnlyDictionary<string, string>? headers, CancellationToken ct)
        {
            _reached.TrySetResult();
            await _gate.WaitAsync(ct);
            return true;
        }

        public bool TryStart(AiStartupProcessRequest request, out string? error)
        {
            error = null;
            return true;
        }

        public Task<AiStartupModelPreloadResult> PreloadOllamaModelAsync(
            Uri baseUri, AiStartupModelPreloadRequest request, CancellationToken ct)
            => Task.FromResult(new AiStartupModelPreloadResult(true, null));

        public Task<bool?> IsOllamaModelResidentAsync(Uri baseUri, string modelName, CancellationToken ct)
            => Task.FromResult<bool?>(true);

        public Task<AiStartupWarmupResult> WarmupSidecarModelsAsync(
            Uri sidecarBaseUri, IReadOnlyDictionary<string, string>? headers, CancellationToken ct)
            => Task.FromResult(new AiStartupWarmupResult(true, Array.Empty<string>(), null));
    }

    // ── IAiStartedProcessLifetime: neue Mitglieder am echten Dienst ──────────

    [Fact]
    public void Lifetime_neue_mitglieder_ohne_prozesse()
    {
        IAiStartedProcessLifetime lifetime = new AiStartedProcessLifetimeService();

        Assert.False(lifetime.HasTrackedStartedProcesses);
        Assert.False(lifetime.HasTrackedSidecarProcess);
        Assert.False(lifetime.IsTrackedProcess(1234));
        Assert.Null(lifetime.GetTrackedProcessInfo(1234));
        // No-op, darf nicht werfen:
        lifetime.StopTrackedProcess(1234);
    }

    [Fact]
    public void Lifetime_stop_tracked_beendet_nur_den_eigenen_prozess()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var lifetime = new AiStartedProcessLifetimeService();
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
            Assert.True(lifetime.TryTrack(process, AiStartedProcessKind.Sidecar, "powershell", out var error), error);
            Assert.True(lifetime.HasTrackedStartedProcesses);
            Assert.True(lifetime.HasTrackedSidecarProcess);
            Assert.True(lifetime.IsTrackedProcess(process.Id));

            lifetime.StopTrackedProcess(process.Id);

            Assert.True(process.WaitForExit(milliseconds: 5_000), "Der verfolgte Prozess muss gezielt beendet werden.");
            Assert.False(lifetime.HasTrackedStartedProcesses);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            lifetime.StopAllStartedProcesses();
        }
    }
}
