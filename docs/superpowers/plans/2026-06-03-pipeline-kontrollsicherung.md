# Pipeline-Kontrollsicherung Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Der Codiermodus zeigt laufend und ehrlich an, ob die volle Multi-Model-Pipeline (Sidecar/YOLO/DINO/SAM) aktiv ist, und schaltet sich automatisch in den Vollmodus, sobald der Sidecar bereit ist — ohne App-Neustart.

**Architecture:** Ein schichtenreiner Aufbau: reine Auswertungslogik (`PipelineHealthEvaluator`) + Statusmodell (`PipelineHealthStatus`) in der Application-Schicht; ein Polling-Service (`PipelineHealthMonitor`) + detaillierter Health-Client in der Infrastructure-Schicht; eine Ampel + ausklappbare Details im Player. Der Evaluator nimmt einen reinen Application-Input (`PipelineHealthInputs`), damit Application nicht von Infrastructure abhaengt.

**Tech Stack:** C# / .NET 8, WPF, xUnit. Datenquelle ist der bestehende `/health`-Endpoint des Python-Sidecars (keine Sidecar-Aenderung).

---

## File Structure

- **Create** `src/AuswertungPro.Next.Application/Ai/PipelineHealthStatus.cs` — Statusmodell: Ampel-Level (`Full/Degraded/Down`) + Detail-Flags + Texte. Reines Datenobjekt.
- **Create** `src/AuswertungPro.Next.Application/Ai/PipelineHealthInputs.cs` — reiner Eingabe-Record fuer den Evaluator (entkoppelt von HTTP/DTO).
- **Create** `src/AuswertungPro.Next.Application/Ai/PipelineHealthEvaluator.cs` — reine Logik `Inputs -> Status`. Keine Timer/HTTP/UI.
- **Create** `src/AuswertungPro.Next.Application/Ai/IPipelineHealthMonitor.cs` — Interface des Polling-Service.
- **Modify** `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineDtos.cs` — `GpuStatus` um `loaded_models` erweitern (fuer Detail-Anzeige).
- **Modify** `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineClient.cs` — `PipelineHealthCheckResult` + `CheckHealthDetailedAsync` (unterscheidet offline / 401 / ok). Alte `HealthCheckAsync` bleibt.
- **Create** `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/PipelineHealthMonitor.cs` — Implementierung: 5s-Polling, mappt Client-Ergebnis auf `PipelineHealthInputs`, ruft Evaluator, feuert `StatusChanged`.
- **Modify** `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs` — Monitor starten/stoppen, `_codingUseMultiModel` automatisch nachfuehren, Ampel + Details aktualisieren.
- **Modify** `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml` — ausklappbare Detailanzeige neben der bestehenden Statuszeile.
- **Create** `tests/AuswertungPro.Next.Pipeline.Tests/PipelineHealthEvaluatorTests.cs` — Unit-Tests der Auswertungslogik.

---

## Task 1: Statusmodell + Eingabe-Record (Application)

**Files:**
- Create: `src/AuswertungPro.Next.Application/Ai/PipelineHealthStatus.cs`
- Create: `src/AuswertungPro.Next.Application/Ai/PipelineHealthInputs.cs`

- [ ] **Step 1: PipelineHealthStatus.cs schreiben**

```csharp
namespace AuswertungPro.Next.Application.Ai;

/// <summary>Ampel-Stufe der KI-Pipeline im Codiermodus.</summary>
public enum PipelineHealthLevel
{
    /// <summary>Volle Multi-Model-Pipeline aktiv (gruen).</summary>
    Full,
    /// <summary>Schwachmodus: nur Qwen verfuegbar (gelb).</summary>
    Degraded,
    /// <summary>KI aus oder gar nichts nutzbar (rot/grau).</summary>
    Down
}

/// <summary>
/// Ehrlicher Momentanzustand der KI-Pipeline. Reines Datenobjekt fuer UI + Monitor.
/// </summary>
public sealed record PipelineHealthStatus(
    PipelineHealthLevel Level,
    bool MultiModelActive,
    bool SidecarReachable,
    bool TokenValid,
    bool SidecarHealthy,
    bool QwenAvailable,
    bool YoloLoaded,
    bool DinoLoaded,
    bool SamLoaded,
    string Summary,
    string Detail)
{
    /// <summary>True, solange ueberhaupt eine KI-Analyse moeglich ist (Full oder Degraded).</summary>
    public bool AnalysisPossible => Level != PipelineHealthLevel.Down;
}
```

- [ ] **Step 2: PipelineHealthInputs.cs schreiben**

```csharp
namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Reine Eingabe fuer den <see cref="PipelineHealthEvaluator"/>. Entkoppelt die Logik
/// von HTTP/DTO-Details, damit die Application-Schicht nicht von Infrastructure abhaengt.
/// </summary>
public sealed record PipelineHealthInputs(
    bool AiEnabled,
    bool SidecarReachable,
    bool TokenValid,
    bool SidecarHealthy,
    bool QwenAvailable,
    bool YoloLoaded = false,
    bool DinoLoaded = false,
    bool SamLoaded = false);
```

- [ ] **Step 3: Build der Application-Assembly**

Run: `dotnet build src/AuswertungPro.Next.Application/AuswertungPro.Next.Application.csproj -clp:ErrorsOnly -nologo`
Expected: 0 Fehler.

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/PipelineHealthStatus.cs src/AuswertungPro.Next.Application/Ai/PipelineHealthInputs.cs
git commit -m "feat(pipeline): Statusmodell + Eingabe-Record fuer Health-Auswertung"
```

---

## Task 2: PipelineHealthEvaluator (TDD, reine Logik)

**Files:**
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/PipelineHealthEvaluatorTests.cs`
- Create: `src/AuswertungPro.Next.Application/Ai/PipelineHealthEvaluator.cs`

- [ ] **Step 1: Failing tests schreiben**

```csharp
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class PipelineHealthEvaluatorTests
{
    private static PipelineHealthInputs Base(
        bool ai = true, bool reach = true, bool token = true, bool healthy = true, bool qwen = true,
        bool yolo = true, bool dino = true, bool sam = true)
        => new(ai, reach, token, healthy, qwen, yolo, dino, sam);

    [Fact]
    public void KiDeaktiviert_ergibt_Down()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(ai: false));
        Assert.Equal(PipelineHealthLevel.Down, s.Level);
        Assert.False(s.MultiModelActive);
    }

    [Fact]
    public void SidecarOk_und_TokenOk_ergibt_Full()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base());
        Assert.Equal(PipelineHealthLevel.Full, s.Level);
        Assert.True(s.MultiModelActive);
    }

    [Fact]
    public void SidecarOffline_aber_Qwen_ergibt_Degraded()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(reach: false, healthy: false));
        Assert.Equal(PipelineHealthLevel.Degraded, s.Level);
        Assert.False(s.MultiModelActive);
        Assert.Contains("Sidecar", s.Detail);
    }

    [Fact]
    public void Token401_aber_Qwen_ergibt_Degraded_mit_TokenHinweis()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(token: false));
        Assert.Equal(PipelineHealthLevel.Degraded, s.Level);
        Assert.Contains("Token", s.Detail);
    }

    [Fact]
    public void Token401_ohne_Qwen_ergibt_Down()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(token: false, qwen: false));
        Assert.Equal(PipelineHealthLevel.Down, s.Level);
    }

    [Fact]
    public void SidecarOffline_ohne_Qwen_ergibt_Down()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(reach: false, healthy: false, qwen: false));
        Assert.Equal(PipelineHealthLevel.Down, s.Level);
    }

    [Fact]
    public void ModelleNochNichtGeladen_bleibt_Full_mit_LazyHinweis()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(yolo: false, dino: false, sam: false));
        Assert.Equal(PipelineHealthLevel.Full, s.Level);
        Assert.True(s.MultiModelActive);
        Assert.Contains("Bedarf", s.Detail);
    }

    [Fact]
    public void SidecarErreichbar_aber_nicht_healthy_ergibt_Degraded()
    {
        var s = PipelineHealthEvaluator.Evaluate(Base(healthy: false));
        Assert.Equal(PipelineHealthLevel.Degraded, s.Level);
    }
}
```

- [ ] **Step 2: Tests laufen lassen → muessen fehlschlagen (Evaluator fehlt)**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter PipelineHealthEvaluatorTests -nologo`
Expected: Compile-Fehler / FAIL (Typ `PipelineHealthEvaluator` existiert nicht).

- [ ] **Step 3: Evaluator implementieren**

```csharp
namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Reine Auswertung: wandelt <see cref="PipelineHealthInputs"/> in einen
/// <see cref="PipelineHealthStatus"/>. Keine Seiteneffekte, voll testbar.
///
/// Ampel-Regeln (siehe Spec 2026-06-03):
/// - KI aus -> Down.
/// - Sidecar erreichbar + healthy + Token ok -> Full (Multi-Model).
/// - Sidecar nicht nutzbar (offline / Token / unhealthy), aber Qwen da -> Degraded.
/// - Sidecar nicht nutzbar und kein Qwen -> Down.
/// - Modelle wegen Lazy-Loading noch nicht resident -> bleibt Full, Detail "laedt bei Bedarf".
/// </summary>
public static class PipelineHealthEvaluator
{
    public static PipelineHealthStatus Evaluate(PipelineHealthInputs i)
    {
        if (!i.AiEnabled)
            return new PipelineHealthStatus(
                PipelineHealthLevel.Down, false,
                i.SidecarReachable, i.TokenValid, i.SidecarHealthy, i.QwenAvailable,
                i.YoloLoaded, i.DinoLoaded, i.SamLoaded,
                "Kuenstliche Intelligenz deaktiviert",
                "KI ist in den Einstellungen aus.");

        bool sidecarUsable = i.SidecarReachable && i.SidecarHealthy && i.TokenValid;

        if (sidecarUsable)
        {
            bool allLoaded = i.YoloLoaded && i.DinoLoaded && i.SamLoaded;
            var detail = allLoaded
                ? "YOLO + DINO + SAM aktiv."
                : "Pipeline bereit. Modelle laden bei Bedarf.";
            return new PipelineHealthStatus(
                PipelineHealthLevel.Full, true,
                true, true, true, i.QwenAvailable,
                i.YoloLoaded, i.DinoLoaded, i.SamLoaded,
                "KI bereit (Multi-Model)", detail);
        }

        // Sidecar nicht nutzbar -> Grund bestimmen
        string grund;
        if (!i.SidecarReachable) grund = "Sidecar offline -> keine YOLO/DINO/SAM-Masken.";
        else if (!i.TokenValid) grund = "Sidecar Token ungueltig -> Qwen-only.";
        else grund = "Sidecar antwortet, ist aber nicht gesund -> Qwen-only.";

        if (i.QwenAvailable)
            return new PipelineHealthStatus(
                PipelineHealthLevel.Degraded, false,
                i.SidecarReachable, i.TokenValid, i.SidecarHealthy, true,
                i.YoloLoaded, i.DinoLoaded, i.SamLoaded,
                "KI bereit (Qwen)", grund);

        return new PipelineHealthStatus(
            PipelineHealthLevel.Down, false,
            i.SidecarReachable, i.TokenValid, i.SidecarHealthy, false,
            i.YoloLoaded, i.DinoLoaded, i.SamLoaded,
            "KI nicht verfuegbar", grund);
    }
}
```

- [ ] **Step 4: Tests laufen lassen → muessen passen**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter PipelineHealthEvaluatorTests -nologo`
Expected: 8 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/PipelineHealthEvaluator.cs tests/AuswertungPro.Next.Pipeline.Tests/PipelineHealthEvaluatorTests.cs
git commit -m "feat(pipeline): PipelineHealthEvaluator mit Ampel-Logik (TDD)"
```

---

## Task 3: Health-DTO erweitern + detaillierter Health-Client (Infrastructure)

**Files:**
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineDtos.cs` (GpuStatus)
- Modify: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineClient.cs`

- [ ] **Step 1: GpuStatus um loaded_models erweitern**

In `VisionPipelineDtos.cs` den `GpuStatus`-Record ersetzen durch:

```csharp
public sealed record GpuStatus(
    [property: JsonPropertyName("current_model")] string CurrentModel,
    [property: JsonPropertyName("vram_allocated_gb")] double VramAllocatedGb,
    [property: JsonPropertyName("vram_total_gb")] double VramTotalGb,
    [property: JsonPropertyName("loaded_models")] Dictionary<string, GpuLoadedModel>? LoadedModels = null
);

public sealed record GpuLoadedModel(
    [property: JsonPropertyName("device")] string? Device = null,
    [property: JsonPropertyName("load_time_sec")] double LoadTimeSec = 0
);
```

- [ ] **Step 2: PipelineHealthCheckResult + CheckHealthDetailedAsync im Client**

In `VisionPipelineClient.cs` einen neuen Result-Record (am Dateiende, im selben namespace) hinzufuegen:

```csharp
/// <summary>
/// Detailliertes Ergebnis eines Health-Checks. Unterscheidet offline / 401 / ok,
/// damit die UI Token-Fehler nicht als "offline" anzeigt.
/// </summary>
public sealed record PipelineHealthCheckResult(
    bool IsReachable,
    bool IsAuthorized,
    int? StatusCode,
    SidecarHealthResponse? Health,
    string? Error);
```

Und eine neue Methode in der Klasse `VisionPipelineClient` (neben `HealthCheckAsync`):

```csharp
/// <summary>
/// Health-Check mit Fehlerart-Unterscheidung (offline vs. 401 vs. ok).
/// </summary>
public async Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
{
    try
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, BuildUri("/health"));
        AddSidecarTokenHeader(req);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);

        int code = (int)resp.StatusCode;
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return new PipelineHealthCheckResult(true, false, code, null, "Token ungueltig/fehlt");

        if (!resp.IsSuccessStatusCode)
            return new PipelineHealthCheckResult(true, true, code, null, $"HTTP {code}");

        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var health = JsonSerializer.Deserialize<SidecarHealthResponse>(json, JsonOpts);
        return new PipelineHealthCheckResult(true, true, code, health, null);
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex)
    {
        return new PipelineHealthCheckResult(false, false, null, null, ex.Message);
    }
}
```

- [ ] **Step 3: Build Infrastructure**

Run: `dotnet build src/AuswertungPro.Next.Infrastructure/AuswertungPro.Next.Infrastructure.csproj -clp:ErrorsOnly -nologo`
Expected: 0 Fehler. (Hinweis: `OperationCanceledException` darf nicht als allgemeiner Fehler geschluckt werden — deshalb der eigene catch.)

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineDtos.cs src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineClient.cs
git commit -m "feat(pipeline): detaillierter Health-Check (offline/401/ok) + loaded_models DTO"
```

---

## Task 4: IPipelineHealthMonitor + PipelineHealthMonitor

**Files:**
- Create: `src/AuswertungPro.Next.Application/Ai/IPipelineHealthMonitor.cs`
- Create: `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/PipelineHealthMonitor.cs`

- [ ] **Step 1: Interface schreiben (Application)**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Pollt den KI-Pipeline-Zustand und meldet Aenderungen. Fuehrt keine UI-Aenderung aus.
/// </summary>
public interface IPipelineHealthMonitor : IAsyncDisposable
{
    PipelineHealthStatus CurrentStatus { get; }
    event EventHandler<PipelineHealthStatus>? StatusChanged;
    void Start();
    Task StopAsync();
    Task<PipelineHealthStatus> RefreshOnceAsync(CancellationToken ct = default);
}
```

- [ ] **Step 2: PipelineHealthMonitor implementieren (Infrastructure)**

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// 5s-Polling des Sidecar-Health. Mappt das Client-Ergebnis auf PipelineHealthInputs,
/// wertet via PipelineHealthEvaluator aus und feuert StatusChanged bei Aenderung.
/// </summary>
public sealed class PipelineHealthMonitor : IPipelineHealthMonitor
{
    private readonly VisionPipelineClient _client;
    private readonly Func<bool> _aiEnabled;
    private readonly Func<bool> _qwenAvailable;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public PipelineHealthMonitor(
        VisionPipelineClient client,
        Func<bool> aiEnabled,
        Func<bool> qwenAvailable,
        TimeSpan? interval = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _aiEnabled = aiEnabled ?? (() => true);
        _qwenAvailable = qwenAvailable ?? (() => true);
        _interval = interval ?? TimeSpan.FromSeconds(5);
        CurrentStatus = PipelineHealthEvaluator.Evaluate(
            new PipelineHealthInputs(true, false, false, false, _qwenAvailable()));
    }

    public PipelineHealthStatus CurrentStatus { get; private set; }
    public event EventHandler<PipelineHealthStatus>? StatusChanged;

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RefreshOnceAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch { /* nie crashen; naechster Tick */ }

            try { await Task.Delay(_interval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<PipelineHealthStatus> RefreshOnceAsync(CancellationToken ct = default)
    {
        bool ai = _aiEnabled();
        bool qwen = _qwenAvailable();

        PipelineHealthInputs inputs;
        if (!ai)
        {
            inputs = new PipelineHealthInputs(false, false, false, false, qwen);
        }
        else
        {
            var r = await _client.CheckHealthDetailedAsync(ct).ConfigureAwait(false);
            bool healthy = r.Health is { Status: "ok" };
            var loaded = r.Health?.Gpu?.LoadedModels;
            bool Has(string k) => loaded != null && loaded.Keys.Any(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase));
            inputs = new PipelineHealthInputs(
                AiEnabled: true,
                SidecarReachable: r.IsReachable,
                TokenValid: r.IsAuthorized,
                SidecarHealthy: healthy,
                QwenAvailable: qwen,
                YoloLoaded: Has("yolo"),
                DinoLoaded: Has("dino"),
                SamLoaded: Has("sam"));
        }

        var status = PipelineHealthEvaluator.Evaluate(inputs);
        if (status != CurrentStatus)
        {
            CurrentStatus = status;
            StatusChanged?.Invoke(this, status);
        }
        return status;
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        _cts.Cancel();
        if (_loop is not null) { try { await _loop.ConfigureAwait(false); } catch { } }
        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
```

- [ ] **Step 3: Build der Solution**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly -nologo`
Expected: 0 Fehler. (`PipelineHealthStatus` ist record → Wertgleichheit, daher `status != CurrentStatus` funktioniert fuer Change-Detection.)

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.Application/Ai/IPipelineHealthMonitor.cs src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/PipelineHealthMonitor.cs
git commit -m "feat(pipeline): PipelineHealthMonitor mit 5s-Polling und Change-Events"
```

---

## Task 5: Player-Integration (Auto-Recovery + Status)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs`

Kontext: `InitCodingAi()` (ca. Zeile 2565-2625) setzt heute `_codingUseMultiModel` einmalig per `HealthCheckAsync`. Das wird durch den Monitor ersetzt. Vorhandene Felder: `_codingVisionClient`, `_codingMultiModel`, `_codingUseMultiModel`, `_codingAiModelName`. Vorhandene UI-Helfer: `SetCodingAiState(status, color, stage, pulse)`.

- [ ] **Step 1: Monitor-Feld + AiEnabled-Cache ergaenzen**

Bei den Coding-Feldern (nahe `private bool _codingUseMultiModel;`, ca. Zeile 88) ergaenzen:

```csharp
private AuswertungPro.Next.Application.Ai.IPipelineHealthMonitor? _codingHealthMonitor;
private bool _codingAiEnabled;
```

- [ ] **Step 2: In InitCodingAi den Monitor aufsetzen statt Einmal-Check**

Den `try`-Block "Multi-Model Pipeline ... initialisieren" (ca. Zeile 2590-2616) ersetzen durch:

```csharp
            // Multi-Model Pipeline (YOLO -> DINO -> SAM): Monitor statt Einmal-Check.
            try
            {
                var sidecarUrl = Environment.GetEnvironmentVariable("SEWERSTUDIO_SIDECAR_URL")
                    ?? "http://localhost:8100";
                _codingVisionClient = new VisionPipelineClient(new Uri(sidecarUrl));
                _codingMultiModel = new SingleFrameMultiModelService(_codingVisionClient);
                _codingAiEnabled = true;

                _codingHealthMonitor = new PipelineHealthMonitor(
                    _codingVisionClient,
                    aiEnabled: () => _codingAiEnabled,
                    qwenAvailable: () => _codingLiveDetection != null || _codingEnhancedVision != null);
                _codingHealthMonitor.StatusChanged += OnPipelineHealthChanged;
                _codingHealthMonitor.Start();

                // Sofort einmal auswerten, damit die Anzeige nicht leer startet.
                var initial = await _codingHealthMonitor.RefreshOnceAsync();
                ApplyPipelineHealth(initial);
            }
            catch (Exception ex)
            {
                _codingUseMultiModel = false;
                SetCodingAiState("Kuenstliche Intelligenz bereit (Qwen)", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"Monitor-Fehler -> {CompactModelName(_codingAiModelName)}: {ex.Message}");
            }
```

- [ ] **Step 3: Event-Handler + Anwendung des Status**

Neue Methoden in derselben Klasse (nahe `InitCodingAi`):

```csharp
    private void OnPipelineHealthChanged(object? sender, AuswertungPro.Next.Application.Ai.PipelineHealthStatus status)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => ApplyPipelineHealth(status));
            return;
        }
        ApplyPipelineHealth(status);
    }

    private void ApplyPipelineHealth(AuswertungPro.Next.Application.Ai.PipelineHealthStatus status)
    {
        // Auto-Recovery: Modus nachfuehren.
        _codingUseMultiModel = status.MultiModelActive;
        if (status.MultiModelActive && _codingMultiModel == null && _codingVisionClient != null)
            _codingMultiModel = new SingleFrameMultiModelService(_codingVisionClient);

        var color = status.Level switch
        {
            AuswertungPro.Next.Application.Ai.PipelineHealthLevel.Full => Color.FromRgb(0x22, 0xC5, 0x5E),     // gruen
            AuswertungPro.Next.Application.Ai.PipelineHealthLevel.Degraded => Color.FromRgb(0xF5, 0x9E, 0x0B), // gelb
            _ => Color.FromRgb(0x94, 0xA3, 0xB8)                                                              // grau
        };
        SetCodingAiState(status.Summary, color, status.Detail);
        BtnCodingAnalyze.IsEnabled = status.AnalysisPossible;
        UpdatePipelineHealthDetails(status); // aus Task 6
    }
```

- [ ] **Step 4: Monitor beim Schliessen/Stoppen sauber beenden**

Im vorhandenen Aufraeum-/Stop-Pfad des Codiermodus (dort wo `_codingMultiModel`/`_codingLiveDetection` zurueckgesetzt werden bzw. im Window-Close-Handler) ergaenzen:

```csharp
        _codingAiEnabled = false;
        if (_codingHealthMonitor != null)
        {
            _codingHealthMonitor.StatusChanged -= OnPipelineHealthChanged;
            _ = _codingHealthMonitor.StopAsync();
            _codingHealthMonitor = null;
        }
```

- [ ] **Step 5: Build der Solution**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly -nologo`
Expected: 0 Fehler. (Falls `UpdatePipelineHealthDetails` noch fehlt: in Task 6 angelegt — bei Bedarf temporaer als leere Methode anlegen, um isoliert zu bauen.)

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs
git commit -m "feat(pipeline): Auto-Recovery + Live-Status im Codiermodus via Monitor"
```

---

## Task 6: Ampel + ausklappbare Detailanzeige (XAML)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml` (nahe `CodingAiDot`/`TxtCodingAiStatus`, ca. Zeile 447-460)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs` (`UpdatePipelineHealthDetails`)

- [ ] **Step 1: Ausklappbaren Detailbereich im XAML ergaenzen**

Direkt nach dem bestehenden Statuszeilen-Block (nach `TxtCodingAiStage`) einen ToggleButton + Popup einfuegen:

```xml
<ToggleButton x:Name="CodingHealthToggle" Content="Details" FontSize="9"
              Margin="8,0,0,0" Padding="4,1" VerticalAlignment="Center"/>
<Popup PlacementTarget="{Binding ElementName=CodingHealthToggle}" Placement="Bottom"
       IsOpen="{Binding IsChecked, ElementName=CodingHealthToggle}"
       StaysOpen="False" AllowsTransparency="True">
    <Border Background="#FF0B1018" BorderBrush="#FF243A46" BorderThickness="1"
            CornerRadius="6" Padding="10">
        <StackPanel x:Name="CodingHealthDetails" MinWidth="220">
            <TextBlock x:Name="Hd_Sidecar" Foreground="#FFD7E3F0" FontSize="11" Text="Sidecar: -"/>
            <TextBlock x:Name="Hd_Token"   Foreground="#FFD7E3F0" FontSize="11" Text="Token: -"/>
            <TextBlock x:Name="Hd_Yolo"    Foreground="#FFD7E3F0" FontSize="11" Text="YOLO: -"/>
            <TextBlock x:Name="Hd_Dino"    Foreground="#FFD7E3F0" FontSize="11" Text="DINO: -"/>
            <TextBlock x:Name="Hd_Sam"     Foreground="#FFD7E3F0" FontSize="11" Text="SAM: -"/>
            <TextBlock x:Name="Hd_Mode"    Foreground="#FF91A8BD" FontSize="10" Margin="0,4,0,0" Text="Modus: -"/>
        </StackPanel>
    </Border>
</Popup>
```

- [ ] **Step 2: UpdatePipelineHealthDetails implementieren**

In `PlayerWindow.Coding.cs`:

```csharp
    private void UpdatePipelineHealthDetails(AuswertungPro.Next.Application.Ai.PipelineHealthStatus s)
    {
        static string OkBad(bool ok) => ok ? "OK" : "fehlt";
        static string Loaded(bool ok) => ok ? "geladen" : "laedt bei Bedarf";
        Hd_Sidecar.Text = $"Sidecar: {(s.SidecarReachable ? (s.SidecarHealthy ? "OK" : "antwortet, ungesund") : "offline")}";
        Hd_Token.Text   = $"Token: {(s.SidecarReachable ? OkBad(s.TokenValid) : "-")}";
        Hd_Yolo.Text    = $"YOLO: {Loaded(s.YoloLoaded)}";
        Hd_Dino.Text    = $"DINO: {Loaded(s.DinoLoaded)}";
        Hd_Sam.Text     = $"SAM: {Loaded(s.SamLoaded)}";
        Hd_Mode.Text    = $"Modus: {(s.MultiModelActive ? "Multi-Model" : (s.QwenAvailable ? "Qwen-only" : "KI aus"))}";
    }
```

- [ ] **Step 3: xaml-binding-checker mental durchgehen**

Pruefen: `CodingHealthToggle`, `Hd_Sidecar`, `Hd_Token`, `Hd_Yolo`, `Hd_Dino`, `Hd_Sam`, `Hd_Mode` existieren als `x:Name` im XAML und werden im Code-Behind exakt so referenziert. ✓

- [ ] **Step 4: Build + alle Pipeline-Tests**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly -nologo`
Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --filter PipelineHealthEvaluatorTests -nologo`
Expected: Build 0 Fehler, 8 Tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.cs
git commit -m "feat(pipeline): Ampel + ausklappbare Detailanzeige im Codiermodus"
```

---

## Task 7: Abschluss-Verifikation

- [ ] **Step 1: Voller Build + voller Testlauf des Pipeline-Projekts**

Run: `dotnet build AuswertungPro.sln -clp:ErrorsOnly -nologo`
Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj -nologo`
Expected: 0 Build-Fehler; keine neuen Test-Fehler gegenueber Baseline.

- [ ] **Step 2: Akzeptanzkriterien gegen Code pruefen** (siehe Spec §12) — manuell abhaken.

---

## Self-Review (gegen Spec 2026-06-03)

- §3 Ansatz 2 (Service + Interface): Task 4. ✓
- §4.1 PipelineHealthStatus: Task 1. ✓
- §4.2 PipelineHealthEvaluator (reine Logik, testbar): Task 2. ✓
- §4.3 detaillierter Health-Check (offline/401/ok): Task 3. ✓
- §4.4 IPipelineHealthMonitor (Start/Stop/Refresh/Event): Task 4. ✓
- §4.5 Player-Integration + Auto-Recovery: Task 5. ✓
- §5 Ampel-Logik (Full/Degraded/Down + Lazy): Task 2 (Logik) + Task 5/6 (Anzeige). ✓
- §6 Auto-Recovery 5s + Modus-Nachfuehrung: Task 4 (Intervall) + Task 5 (Nachfuehrung). ✓
- §7 Geltungsbereich nur Codiermodus: Tasks 5/6. ✓
- §8 Tests fuer Evaluator: Task 2. ✓
- Lazy-Loading bleibt Full: Task 2 Test `ModelleNochNichtGeladen_bleibt_Full`. ✓
- Token != offline: Task 3 (Client) + Task 2 Test `Token401...`. ✓

Typkonsistenz geprueft: `Evaluate(PipelineHealthInputs)`, `PipelineHealthStatus`-Felder, `CheckHealthDetailedAsync`/`PipelineHealthCheckResult`, `IPipelineHealthMonitor` (Start/StopAsync/RefreshOnceAsync/StatusChanged/CurrentStatus) durchgaengig identisch verwendet. ✓
