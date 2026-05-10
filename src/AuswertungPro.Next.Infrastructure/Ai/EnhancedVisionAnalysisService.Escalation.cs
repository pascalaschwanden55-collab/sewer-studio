using System;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Ai.Ollama;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.Infrastructure.Ai;

// EnhancedVisionAnalysisService Eskalations-Pfad: AnalyzeWithFastModel
// (8B-Q8 GPU) -> Yellow-Retry mit erweitertem Prompt -> Red-Eskalation
// auf 32B-Hybrid (RAM-Mehrheit). Plus GetEscalationReason-Klassifikator
// (allCodesNull / Severity>=4 / poorQuality). Aus dem Hauptdatei extrahiert
// (Slice 15c).
public sealed partial class EnhancedVisionAnalysisService
{
    public async Task<EnhancedFrameAnalysis> AnalyzeWithContextAsync(
        string framePngBase64,
        MultiModelFrameResult multiModelContext,
        int pipeDiameterMm = 300,
        CancellationToken ct = default)
    {
        LastPipelineWarning = null;
        var contextPrompt = BuildContextPrompt(multiModelContext, pipeDiameterMm);
        var prompt = contextPrompt + "\n\n" + BuildPrompt();

        EnhancedVisionDto dto;
        try
        {
            dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: _model,
                messages:
                [
                    new OllamaClient.ChatMessage(
                        Role: "user",
                        Content: prompt,
                        ImagesBase64: [framePngBase64])
                ],
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (IsStructuredJsonParseFailure(ex))
        {
            // Phase 3.2: Unstrukturierte Ollama-Antwort → poorQuality-Fallback.
            return PoorQualityFallback("Kontextanalyse", _model, ex);
        }
        catch (Exception ex)
        {
            return FailAnalysis("Kontextanalyse", _model, ex);
        }

        return MapToAnalysis(dto);
    }

    public async Task<EnhancedFrameAnalysis> AnalyzeWithFastModelAsync(
        string framePngBase64,
        MultiModelFrameResult multiModelContext,
        int pipeDiameterMm = 300,
        CancellationToken ct = default)
    {
        if (string.Equals(_model, OllamaConfig.DefaultVisionModel, StringComparison.OrdinalIgnoreCase))
            return await AnalyzeWithContextAsync(framePngBase64, multiModelContext, pipeDiameterMm, ct).ConfigureAwait(false);

        return await AnalyzeWithModelAsync(
            OllamaConfig.DefaultVisionModel,
            framePngBase64,
            multiModelContext,
            pipeDiameterMm,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Analysiert mit 8B. Wenn die Erkennung unsicher ist (Yellow/Red),
    /// wird ein Same-Model-Retry mit erweitertem Prompt durchgefuehrt.
    /// Kein Modellwechsel, kein VRAM-Swap — alles im gleichen 8B-Slot.
    /// </summary>
    public async Task<(EnhancedFrameAnalysis Result, bool Escalated)> AnalyzeWithEscalationAsync(
        string framePngBase64,
        MultiModelFrameResult? context,
        int pipeDiameterMm = 300,
        CancellationToken ct = default)
    {
        // 1. Analyse mit 8B (einziges Modell, kein Modellwechsel)
        var first = context != null
            ? await AnalyzeWithContextAsync(framePngBase64, context, pipeDiameterMm, ct).ConfigureAwait(false)
            : await AnalyzeAsync(framePngBase64, ct).ConfigureAwait(false);

        var reason = GetEscalationReason(first);
        if (reason == EscalationReason.None)
            return (first, false);

        // Telemetrie: Retry-Grund zaehlen
        switch (reason)
        {
            case EscalationReason.NoFindings: Interlocked.Increment(ref _retryAllCodesNull); break;
            case EscalationReason.AllCodesNull: Interlocked.Increment(ref _retryAllCodesNull); break;
            case EscalationReason.HighSeverity: Interlocked.Increment(ref _retryHighSeverity); break;
            case EscalationReason.PoorQuality: Interlocked.Increment(ref _retryPoorQuality); break;
        }

        // 2. Same-Model-Retry mit erweitertem Prompt (kein VRAM-Swap noetig!)
        //    Throttle: max 2 gleichzeitige Retries um GPU-Contention zu begrenzen
        if (!await _retryThrottle.WaitAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false))
        {
            System.Diagnostics.Debug.WriteLine(
                "[EnhancedVision] Retry uebersprungen — Throttle-Timeout (zu viele parallele Retries)");
            return (first, false);
        }
        try
        {
            // Erweiterter Retry: gleicher 8B-Slot, aber mit expliziterem Prompt
            var retryResult = await RetryWithEnhancedPromptAsync(
                framePngBase64, first, context, pipeDiameterMm, reason, ct).ConfigureAwait(false);

            Interlocked.Increment(ref _retryCount);
            System.Diagnostics.Debug.WriteLine(
                $"[EnhancedVision] Retry #{_retryCount} ({reason}): " +
                $"{retryResult.Findings.Count} Findings, Quality={retryResult.ImageQuality}");

            // 3. Optional: 32B Swap-Eskalation wenn Same-Model-Retry nicht gereicht hat
            if (!string.IsNullOrEmpty(_referenceModel)
                && !string.Equals(_referenceModel, _model, StringComparison.OrdinalIgnoreCase))
            {
                var retryReason = GetEscalationReason(retryResult);
                if (retryReason is EscalationReason.NoFindings or EscalationReason.AllCodesNull or EscalationReason.HighSeverity)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[EnhancedVision] Same-Model-Retry unzureichend ({retryReason}) — starte 32B Swap-Eskalation");
                    // Serialisierung: nur ein 32B-Call gleichzeitig (CLAUDE.md Phase 3.1).
                    // WaitAsync wirft OperationCanceledException bei ct → propagiert nach oben.
                    await _escalation32BLock.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        var swapResult = await AnalyzeWithModelAsync(_referenceModel, framePngBase64, ct)
                            .ConfigureAwait(false);
                        Interlocked.Increment(ref _swap32bCount);
                        System.Diagnostics.Debug.WriteLine(
                            $"[EnhancedVision] 32B Swap #{_swap32bCount}: {swapResult.Findings.Count} Findings");
                        return (swapResult, true);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex32b)
                    {
                        SetPipelineWarning(
                            $"32B Swap fehlgeschlagen ({_referenceModel}): {ex32b.GetType().Name}: {ex32b.Message} - verwende Retry-Ergebnis");
                        // Best-effort: PipelineFailure-Subscriber darf die
                        // 32B-Eskalation nicht kippen, falls der Handler wirft.
                        try { PipelineFailure?.Invoke(this, new PipelineFailureEvent(
                            "32B-Eskalation", _referenceModel,
                            ex32b.GetType().Name, ex32b.Message, DateTimeOffset.Now)); }
                        catch (Exception evEx) { System.Diagnostics.Debug.WriteLine($"[Escalation] PipelineFailure-Handler: {evEx.Message}"); }
                    }
                    finally
                    {
                        _escalation32BLock.Release();
                    }
                }
            }

            return (retryResult, true);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Retry fehlgeschlagen → Erst-Ergebnis als Fallback
            SetPipelineWarning(
                $"Retry fehlgeschlagen ({ex.GetType().Name}): {ex.Message} - verwende Erst-Ergebnis");
            return (first, false);
        }
        finally
        {
            _retryThrottle.Release();
        }
    }

    /// <summary>
    /// Same-Model-Retry: gleicher 8B-Slot, erweiterter Prompt je nach Fehlergrund.
    /// Nutzt 8192-Kontext um mehr Few-Shots und explizitere Anweisungen zu packen.
    /// </summary>
    private async Task<EnhancedFrameAnalysis> RetryWithEnhancedPromptAsync(
        string framePngBase64,
        EnhancedFrameAnalysis firstResult,
        MultiModelFrameResult? context,
        int pipeDiameterMm,
        EscalationReason reason,
        CancellationToken ct)
    {
        var messages = new List<OllamaClient.ChatMessage>();

        // Few-Shot Beispiele injizieren (8192 ctx erlaubt mehr Beispiele)
        var fewShot = _cachedFewShot;
        if (fewShot is { Count: > 0 })
        {
            foreach (var (example, b64) in fewShot)
            {
                var exPrompt = $"Analysiere dieses Kanalbild. " +
                    $"Hinweis: Dieses Bild zeigt {example.Description}" +
                    (example.ClockPosition != null ? $" bei {example.ClockPosition}" : "") +
                    $" (VSA-Code: {example.VsaCode}).";

                messages.Add(new OllamaClient.ChatMessage(
                    Role: "user", Content: exPrompt, ImagesBase64: [b64]));
                messages.Add(new OllamaClient.ChatMessage(
                    Role: "assistant", Content: BuildFewShotResponse(example)));
            }
        }

        // Erweiterter Prompt basierend auf dem Fehlergrund
        var enhancedHint = reason switch
        {
            EscalationReason.AllCodesNull =>
                "\nWICHTIG: Der erste Versuch konnte keinen VSA-Code zuordnen. " +
                "Pruefe NOCHMAL sorgfaeltig: Rohranfang (BCD), Rohrende (BCE), Anschluss (BCA), " +
                "Bogen (BCC), Riss (BAB), Bruch (BAC), Wurzeln (BBA), Ablagerungen (BBC). " +
                "JEDER Befund MUSS einen gültigen VSA-Code haben.",
            EscalationReason.HighSeverity =>
                "\nWICHTIG: Hoher Schweregrad erkannt. Pruefe NOCHMAL sorgfaeltig: " +
                "Ist es wirklich severity 4-5? Verwechslung mit Rohranfang (BCD, severity=1) ausschliessen. " +
                "Quantifiziere GENAU: Ausdehnung %, Querschnittsverringerung %, Uhrlage.",
            EscalationReason.PoorQuality =>
                "\nWICHTIG: Schlechte Bildqualitaet. Beschreibe NUR was SICHER erkennbar ist. " +
                "Im Zweifel: severity NICHT uebertreiben, lieber konservativ bewerten.",
            _ => ""
        };

        var prompt = BuildPrompt() + enhancedHint;

        // Context von YOLO/DINO mitgeben falls vorhanden
        if (context != null)
        {
            var contextPrompt = BuildContextPrompt(context, pipeDiameterMm);
            prompt = contextPrompt + "\n\n" + prompt;
        }

        messages.Add(new OllamaClient.ChatMessage(
            Role: "user", Content: prompt, ImagesBase64: [framePngBase64]));

        EnhancedVisionDto dto;
        try
        {
            dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: _model,
                messages: messages,
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (IsStructuredJsonParseFailure(ex))
        {
            // Phase 3.2: Unstrukturierte Ollama-Antwort beim Retry → poorQuality-Fallback,
            // statt die Exception bis in den Batch hochpropagieren zu lassen.
            return PoorQualityFallback("Retry mit erweitertem Prompt", _model, ex);
        }

        return MapToAnalysis(dto);
    }

    /// <summary>Grund fuer die Eskalation (fuer Telemetrie).</summary>
    private enum EscalationReason { None, NoFindings, AllCodesNull, HighSeverity, PoorQuality }

    /// <summary>
    /// Prueft ob eine Eskalation zum Reference-Modell noetig ist.
    /// Gibt den konkreten Grund zurueck (fuer Telemetrie-Zaehler).
    /// </summary>
    private static EscalationReason GetEscalationReason(EnhancedFrameAnalysis fast)
    {
        if (fast.IsEmptyFrame) return EscalationReason.None;

        // Kein leerer Frame, aber keine Findings: fuer BBox-/Einzelframe-Analyse kritisch.
        if (!fast.HasFindings) return EscalationReason.NoFindings;

        // Alle Findings ohne VSA-Code → 8B hat keine Zuordnung gefunden
        if (fast.Findings.All(f => string.IsNullOrEmpty(f.VsaCodeHint)))
            return EscalationReason.AllCodesNull;

        // Hoher Schweregrad → Genauigkeit kritisch
        if (fast.Findings.Any(f => f.Severity >= 4))
            return EscalationReason.HighSeverity;

        // Schlechte Bildqualitaet mit Findings → unsichere Erkennung
        // "schlecht" statt "mittel" — "mittel" ist Normalfall bei Kanalvideos und wuerde zu breit triggern
        if (fast.ImageQuality == "schlecht" && fast.HasFindings)
            return EscalationReason.PoorQuality;

        return EscalationReason.None;
    }

    /// <summary>
    /// Analysiert mit dem Reference-Modell (32B, komplett RAM mit num_gpu=0).
    /// Kein VRAM-Konflikt — 32B laeuft permanent im RAM neben 8B auf GPU.
    /// </summary>
    private async Task<EnhancedFrameAnalysis> AnalyzeWithModelAsync(
        string model,
        string framePngBase64,
        CancellationToken ct)
    {
        var messages = BuildMessages(framePngBase64);
        try
        {
            // num_gpu=10: hybrid GPU/RAM (~9s statt 28s, CPU-Last sinkt deutlich)
            var referenceOptions = new Dictionary<string, object> { ["num_gpu"] = 10 };
            var dto = await _client.ChatStructuredWithOptionsAsync<EnhancedVisionDto>(
                model: model,
                messages: messages,
                formatSchema: EnhancedVisionSchema,
                options: referenceOptions,
                ct: ct).ConfigureAwait(false);
            return MapToAnalysis(dto);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (IsStructuredJsonParseFailure(ex))
        {
            // Phase 3.2: Unstrukturierte Ollama-Antwort vom Reference-Modell → poorQuality-Fallback.
            return PoorQualityFallback("Modell-Eskalation", model, ex);
        }
        catch (Exception ex)
        {
            return FailAnalysis("Modell-Eskalation", model, ex);
        }
    }

    /// <summary>
    /// Analysiert mit spezifischem Modell und Multi-Model-Kontext (fuer Eskalation).
    /// </summary>
    private async Task<EnhancedFrameAnalysis> AnalyzeWithModelAsync(
        string model,
        string framePngBase64,
        MultiModelFrameResult multiModelContext,
        int pipeDiameterMm,
        CancellationToken ct)
    {
        var contextPrompt = BuildContextPrompt(multiModelContext, pipeDiameterMm);
        var prompt = contextPrompt + "\n\n" + BuildPrompt();
        try
        {
            var dto = await _client.ChatStructuredAsync<EnhancedVisionDto>(
                model: model,
                messages:
                [
                    new OllamaClient.ChatMessage(
                        Role: "user",
                        Content: prompt,
                        ImagesBase64: [framePngBase64])
                ],
                formatSchema: EnhancedVisionSchema,
                ct: ct).ConfigureAwait(false);
            return MapToAnalysis(dto);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (IsStructuredJsonParseFailure(ex))
        {
            // Phase 3.2: Unstrukturierte Ollama-Antwort vom Reference-Modell → poorQuality-Fallback.
            return PoorQualityFallback("Modell-Eskalation mit Kontext", model, ex);
        }
        catch (Exception ex)
        {
            return FailAnalysis("Modell-Eskalation mit Kontext", model, ex);
        }
    }
}
