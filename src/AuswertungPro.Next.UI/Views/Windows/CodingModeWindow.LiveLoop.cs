using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

// Slice 8a.3 Step 3: Loop-Orchestrator als CodingModeWindow-Partial.
//
// Diese Datei fuegt einen neuen, additiven Code-Pfad hinzu: die
// Live-Coding-Loop, die im PlayerWindow heute als RunCodingAnalysisAsync
// existiert. Sie fuetterte Frame fuer Frame durch Capture → Vision →
// Render. Hier wandert sie ans CodingModeWindow und nutzt die in Step 2a
// migrierte Frame-Readiness-API (VM-Owner).
//
// Step 3 ist BEWUSST additiv: kein bestehender Caller wird umgebogen.
// Die Methode existiert, wird aber noch nicht aufgerufen. Step 4 leitet
// dann den Single-Frame-Pfad (BtnAnalyzeFrame_Click) ueber RunLiveAnalysis-
// Async mit oneShot=true. Step 5 entfernt PlayerWindow-Pendant.
//
// Pipeline-Variante: Hier wird die einfache Qwen-/EnhancedVision-Pfad
// genutzt (wie der heutige AnalyzeCurrentFrameAsync). Die YOLO-first/
// SAM/Multi-Model-Eskalation aus PlayerWindow bleibt fuer eine spaetere
// Iteration. Verhalten: identisch zum heutigen Single-Frame-Pfad,
// nur in einer Schleife mit Frame-Readiness-Gate davor.
public partial class CodingModeWindow
{
    /// <summary>
    /// Live-Coding-Loop: capture → vision → readiness-gate → render,
    /// in Endlosschleife bis CancellationToken signalisiert.
    /// Mit oneShot=true wird nach genau einer Iteration zurueckgekehrt
    /// (fuer den Single-Frame-Pfad in Step 4).
    /// </summary>
    /// <param name="ct">Loop-CTS aus dem Caller (Q3 aus Mini-ADR: eine
    /// Loop-CTS pro Coding-Session). Cancel beendet die Schleife sauber.</param>
    /// <param name="oneShot">true → nach erster Iteration return.</param>
    private async Task<bool> RunLiveAnalysisAsync(CancellationToken ct, bool oneShot = false)
    {
        if (_liveDetection == null || _player == null) return false;

        // Cadence zwischen Frames in Millisekunden. ~2.5s entspricht der
        // typischen Qwen-3-VL-8B-Q8-Inferenzzeit; gibt der State-Maschine
        // Zeit, OSD-Meter zu bestaetigen ohne CPU/GPU zu pruegeln.
        const int LoopDelayMs = 2500;
        const int RetryDelayMs = 1500; // wenn Capture fehlschlaegt

        do
        {
            if (ct.IsCancellationRequested) break;

            // ── Capture ──
            var pngBytes = await CaptureCurrentFrameAsync();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                if (oneShot) return false;
                try { await Task.Delay(RetryDelayMs, ct); } catch (OperationCanceledException) { return false; }
                continue;
            }

            // ── Vision ──
            var timestampSec = _player.Time / 1000.0;
            LiveDetection result;
            try
            {
                if (_enhancedVision != null)
                {
                    var b64 = Convert.ToBase64String(pngBytes);
                    var (enhanced, _) = await _enhancedVision.AnalyzeWithEscalationAsync(
                        b64, context: null, ct: ct);
                    result = LiveDetectionMapper.FromEnhancedAnalysis(enhanced, timestampSec);

                    // Sichtbares Panel fuer User - vorher: Result war oft nur in der Liste.
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"Bildqualitaet: {enhanced.ImageQuality}");
                        sb.AppendLine($"Material: {enhanced.PipeMaterial}");
                        if (enhanced.PipeDiameterMm.HasValue)
                            sb.AppendLine($"DN geschaetzt: {enhanced.PipeDiameterMm} mm");
                        sb.AppendLine($"Findings: {enhanced.Findings.Count}");
                        foreach (var f in enhanced.Findings.Take(3))
                        {
                            sb.AppendLine($"• {f.Label}");
                            if (!string.IsNullOrEmpty(f.VsaCodeHint))
                                sb.AppendLine($"  Code: {f.VsaCodeHint}  | Sev: {f.Severity}");
                        }
                        if (!string.IsNullOrWhiteSpace(_enhancedVision.LastPipelineWarning))
                            sb.AppendLine($"Warnung: {_enhancedVision.LastPipelineWarning}");
                        if (string.IsNullOrEmpty(enhanced.Error))
                            ShowBboxResultPanel("Frame-Analyse", sb.ToString(), isError: false);
                        else
                            ShowBboxResultPanel("Analyse-Fehler", enhanced.Error, isError: true);
                    });
                }
                else
                {
                    result = await _liveDetection.AnalyzeFrameAsync(pngBytes, timestampSec, ct);
                }
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                    SetAiStatus($"Fehler: {ex.Message}", "#EF4444",
                        $"Modell: {CompactModelName(_aiModelName)}", error: true));
                if (oneShot) return false;
                try { await Task.Delay(RetryDelayMs, ct); } catch (OperationCanceledException) { return false; }
                continue;
            }

            // ── Frame-Readiness Gate (VM-API aus Step 2a) ──
            _vm.RecordFrame(result);
            if (!_vm.IsFrameReady)
            {
                if (result.Findings.Count > 0)
                    _vm.PendingWarmupResult = result;

                await Dispatcher.InvokeAsync(() =>
                    SetAiStatus("Dateneinblendung — uebersprungen",
                        "#94A3B8",
                        $"Warte auf Videobild... (Bild {_vm.OsdSkippedFrames} von 3)"));

                if (oneShot) return false;
                try { await Task.Delay(RetryDelayMs, ct); } catch (OperationCanceledException) { return false; }
                continue;
            }

            // ── Warmup-Puffer einarbeiten (erste Ready-Transition) ──
            if (_vm.PendingWarmupResult != null)
            {
                var buffered = _vm.PendingWarmupResult;
                _vm.PendingWarmupResult = null;
                if (result.Findings.Count == 0 && buffered.Findings.Count > 0)
                    result = buffered;
            }

            // ── Render ──
            await Dispatcher.InvokeAsync(() => ShowAiResults(result));

            // ── Pause-Confirm-Gate (Slice 8a Pause-Confirm Step 4) ──
            // Erstes Yellow/Red-Finding ans VM melden und auf User-Decision warten.
            // Green-Findings werden im aktuellen Slice nicht automatisch in die
            // Eventliste uebernommen (Auto-BCD/BCE ist im Mini-ADR ausgeklammert).
            try
            {
                var consumed = await PromptConfirmIfNeededAsync(result, timestampSec, ct);
                if (consumed && oneShot) return true;
            }
            catch (OperationCanceledException) { return false; }

            if (oneShot) return true;
            try { await Task.Delay(LoopDelayMs, ct); } catch (OperationCanceledException) { return false; }
        }
        while (!ct.IsCancellationRequested);

        return false;
    }

    /// <summary>Prueft die Findings auf Yellow/Red, pausiert den Player und
    /// haelt den Loop ueber BeginConfirmationAsync auf bis der User
    /// Akzeptieren/Bearbeiten/Verwerfen geklickt hat. Liefert true wenn ein
    /// Finding behandelt wurde (Pause + Confirm-Roundtrip).</summary>
    private async Task<bool> PromptConfirmIfNeededAsync(
        LiveDetection result, double timestampSec, CancellationToken ct)
    {
        var frameMeter = result.MeterReading ?? _vm.LastOsdMeter ?? 0.0;

        // Erstes Yellow/Red-Finding finden, das nicht bereits in der
        // Sperrliste steht (Slice 8a Pause-Confirm Step 5). Sperrliste
        // arbeitet auf dem Code wie ihn BuildCodingEventFromFinding
        // erzeugt — VsaCodeHint, sonst Fallback "AI" — damit der User
        // beim naechsten Frame mit dem gleichen Reject-Schluessel kein
        // Pause-Panel mehr sieht.
        LiveFrameFinding? hit = null;
        bool hitIsRed = false;
        double hitConfidence = 0;
        foreach (var f in result.Findings)
        {
            var (isGreen, isYellow, isRed, conf) = EvaluateGate(f);
            if (isGreen) continue;
            var candidateCode = string.IsNullOrWhiteSpace(f.VsaCodeHint) ? "AI" : f.VsaCodeHint!;
            if (_vm.IsRejected(candidateCode, frameMeter)) continue;
            hit = f;
            hitIsRed = isRed;
            hitConfidence = conf;
            break;
        }
        if (hit is null) return false;

        var videoTs = TimeSpan.FromSeconds(timestampSec);
        var ev = BuildCodingEventFromFinding(hit, frameMeter, videoTs, hitConfidence);

        // Player pausieren, BeginConfirmationAsync awaiten, dann je nach
        // Decision Event aufnehmen oder droppen.
        await Dispatcher.InvokeAsync(() => _player?.SetPause(true));

        CodingUserDecision decision;
        try
        {
            decision = await _vm.BeginConfirmationAsync(ev, hitConfidence, hitIsRed, ct);
        }
        catch (OperationCanceledException)
        {
            // Beim Cancel Loop nicht mehr resumen — Caller schliesst eh ab.
            throw;
        }

        switch (decision)
        {
            case CodingUserDecision.Accepted:
                await Dispatcher.InvokeAsync(() => _vm.AddEventInOrder(ev));
                break;
            case CodingUserDecision.AcceptedWithEdit:
                // Edit-Pfad: Event aufnehmen und sofort die Edit-Affordance
                // surfen. _vm.SelectedDefect wurde in ConfirmEdit_Click gesetzt,
                // aber LstEvents.SelectedItem laeuft separat (kein VM-Binding)
                // und das DefectDetailPanel wird nicht gebunden, sondern via
                // UpdateDefectDetailPanel(ev) manuell befuellt. Per ADR-Q5-A
                // landet der User auf der Edit-Affordance — der modale Editor
                // bleibt einen Klick entfernt.
                await Dispatcher.InvokeAsync(() =>
                {
                    _vm.AddEventInOrder(ev);
                    _vm.SelectedDefect = ev;
                    LstEvents.SelectedItem = ev;
                    LstEvents.ScrollIntoView(ev);
                    UpdateDefectDetailPanel(ev);
                });
                break;
            case CodingUserDecision.Rejected:
                // Slice 8a Pause-Confirm Step 5: in die in-memory Sperrliste
                // aufnehmen, damit das gleiche Finding (Code + Meter +/- 0.5m)
                // im Rest der Session nicht erneut den Pause-Confirm-Workflow
                // triggert. AddRejection ist idempotent + case-insensitive.
                _vm.AddRejection(ev.Entry.Code, ev.MeterAtCapture);
                break;
        }

        // Player wieder laufen lassen.
        await Dispatcher.InvokeAsync(() => _player?.SetPause(false));
        return true;
    }
}
