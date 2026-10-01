using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.VsaCatalog;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// DINO-Schritt der Mehrmodell-Analyse (Grounding-DINO-Detektion, eingeschraenkte Antwort und das
/// Bild ohne Box samt box-losem Grundgeruest-Befund), ausgelagert aus
/// <see cref="MultiModelAnalysisService"/> (AP05b). Eigene Regeln dieses Modells, bewusst nicht mit
/// YOLO/SAM/Qwen gleichgesetzt:
/// VRAM-Mangel ist ein Kapazitaetsfehler, jeder andere Aufruffehler ein Transportfehler, ein
/// Nutzerabbruch wird weitergereicht. Eine als <c>degraded</c> gemeldete Antwort ist ein
/// Modellfehler (nur Skip-Quote) und nie ein sauberer Negativbefund; ihr Fehlercode ersetzt den
/// bisherigen Degraded-Grund im Trace (belegtes Verhalten, bewusst nicht angeglichen).
/// Keine Box ist dagegen ein sauberer Negativbefund, ausser der Klassifikator bestaetigt einen
/// Grundgeruest-Code (Fix #1).
/// </summary>
internal sealed class MultiModelDinoSchritt
{
    private readonly IVisionPipelineClient _client;
    private readonly PipelineConfig _config;
    private readonly ILogger _logger;

    public MultiModelDinoSchritt(IVisionPipelineClient client, PipelineConfig config, ILogger logger)
    {
        _client = client;
        _config = config;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Ist <see cref="Abschluss"/> gesetzt, endet das Bild hier; sonst liegt die Antwort vor.</summary>
    internal sealed record Ergebnis(MultiModelBildErgebnis? Abschluss, DinoResponse? Antwort);

    /// <summary>
    /// Einstellungen fuer den box-losen Grundgeruest-Befund (Fix #1). Der Dienst liest sie je Bild,
    /// weil sie oeffentlich einstellbar sind.
    /// </summary>
    internal readonly record struct GrundgeruestRegel(bool Aktiv, double MinConfidence, double ReichweiteM);

    /// <summary>Step 2 des Bildschritts: Grounding-DINO-Detektion. Setzt <see cref="MultiModelBildKontext.DinoMs"/>.</summary>
    public async Task<Ergebnis> ErkenneAsync(MultiModelLaufZustand run, MultiModelBildKontext bild, CancellationToken ct)
    {
        var trace = bild.Trace;
        run.Progress?.Report(new VideoAnalysisProgress(run.FrameIndex, run.TotalFrames,
            $"Frame {run.FrameIndex}/{run.TotalFrames} – Grounding DINO Detection...",
            FramePreviewPng: bild.FrameBytes));

        var phaseSw = Stopwatch.StartNew();
        var aufruf = await MultiModelSidecarAufruf.AusfuehrenAsync(() => _client.DetectDinoAsync(
            new DinoRequest(
                bild.FrameBase64,
                null, // use default labels from sidecar config
                _config.DinoBoxThreshold,
                _config.DinoTextThreshold), ct), ct).ConfigureAwait(false);
        if (aufruf.Fehlerart == MultiModelFehlerart.Kapazitaet)
        {
            // Paket 2/A4: VRAM-Mangel = Kapazitaetsfehler, KEIN Transport-Ausfall:
            // kein Outage-Zaehler, kein Neustart — wie ein Modellfehler ueberspringen
            // (Skip-Quote + Incomplete); das Checkpoint-Journal schreibt weiter retry_required.
            var ex = aufruf.Fehler!;
            _logger.LogWarning(ex, "Frame {Frame}: DINO wegen VRAM-Mangels uebersprungen", run.FrameIndex);
            run.Progress?.Report(new VideoAnalysisProgress(run.FrameIndex, run.TotalFrames,
                $"Frame {run.FrameIndex} – DINO übersprungen: {ex.Message}"));
            bild.RecordFrame(run, bild.YoloMs, phaseSw.ElapsedMilliseconds, 0);
            trace.Path = "dino_error";
            trace.DropReason = "vram_insufficient";
            MultiModelAnalysisService.MarkTraceDegraded(trace, "vram_insufficient");
            return new Ergebnis(MultiModelBildErgebnis.VramMangel(ex.Message, bild.EstimatedMeter), null);
        }
        if (aufruf.Fehlerart == MultiModelFehlerart.Transport)
        {
            var ex = aufruf.Fehler!;
            _logger.LogWarning(ex, "Frame {Frame}: DINO detection failed", run.FrameIndex);
            run.Progress?.Report(new VideoAnalysisProgress(run.FrameIndex, run.TotalFrames,
                $"Frame {run.FrameIndex} – DINO Fehler: {ex.Message}"));
            bild.RecordFrame(run, bild.YoloMs, phaseSw.ElapsedMilliseconds, 0);
            return new Ergebnis(MultiModelBildErgebnis.Transportfehler("dino_error", bild.EstimatedMeter), null);
        }
        var dinoResult = aufruf.Antwort!;
        bild.DinoMs = phaseSw.ElapsedMilliseconds;
        trace.DinoBoxCount = dinoResult.Detections.Count;

        // degraded != sauber: ein Modell-/Inferenzfehler im Sidecar (degraded=true)
        // darf NICHT als "dino_no_boxes" (kein Befund) verbucht werden, sonst sieht
        // ein verstummtes Modell wie ein sauberes Rohr aus. Frame als Review markieren.
        if (dinoResult.Degraded)
        {
            _logger.LogWarning("Frame {Frame}: DINO degraded ({Code}: {Error}) – als Review markiert, NICHT als sauberer Negativbefund.",
                run.FrameIndex, dinoResult.ErrorCode, dinoResult.Error);
            run.Progress?.Report(new VideoAnalysisProgress(run.FrameIndex, run.TotalFrames,
                $"Frame {run.FrameIndex} – DINO degraded (Modellfehler) – Review nötig"));
            bild.RecordFrame(run, bild.YoloMs, bild.DinoMs, 0);
            trace.Path = "dino_degraded";
            trace.DropReason = "dino_degraded";
            // Anhaengen statt ersetzen (seit 01.10.2026, Befund 4 aus AP05): Ein vorher
            // gesetzter Grund wie "detector_unqualified" bleibt im Trace erhalten.
            MultiModelAnalysisService.MarkTraceDegraded(trace, dinoResult.ErrorCode ?? "dino_degraded");
            // Modellfehler: nur Skip-Quote, kein Transport-Ausfall
            return new Ergebnis(MultiModelBildErgebnis.Modellfehler(bild.EstimatedMeter), null);
        }

        return new Ergebnis(null, dinoResult);
    }

    /// <summary>
    /// DINO fand keine Box. Fix #1: Bevor der Frame verworfen wird — wenn der Klassifikator einen
    /// Grundgeruest-Code (BCA/BCC/BCD/BCE) ueber das Voting bestaetigt, einen box-losen Befund
    /// erzeugen. Rettet Bestandsaufnahme, die DINO nicht boxt. Sonst sauberer Negativbefund.
    /// </summary>
    public MultiModelBildErgebnis OhneBox(MultiModelLaufZustand run, MultiModelBildKontext bild,
        YoloClassifyResponse? clsResult, double meterNoBox, GrundgeruestRegel regel)
    {
        var trace = bild.Trace;
        EnhancedFinding? structuralOnly = null;
        if (regel.Aktiv
            && clsResult is { Predictions.Count: > 0 }
            && MultiModelAnalysisService.CanUseClassifierDecision(clsResult))
        {
            var resolved = ClassifierOnlyStructuralPolicy.TryResolve(
                clsResult.Predictions, meterNoBox, regel.ReichweiteM,
                isBend: clsResult.IsBend, minConfidence: regel.MinConfidence);
            if (resolved is not null)
            {
                var confirmed = run.CodeVoting.RegisterAndVote(resolved.Code, meterNoBox);
                if (confirmed is not null)
                {
                    structuralOnly = new EnhancedFinding(
                        Label: VsaCodeTree.LookupLabel(confirmed) ?? confirmed,
                        VsaCodeHint: confirmed,
                        Severity: 1,
                        PositionClock: null,
                        ExtentPercent: null, HeightMm: null, WidthMm: null,
                        IntrusionPercent: null, CrossSectionReductionPercent: null,
                        DiameterReductionMm: null,
                        BboxX1: null, BboxY1: null, BboxX2: null, BboxY2: null,
                        Notes: $"classifier-only (DINO 0 Boxen), conf={resolved.Confidence:F2}, {resolved.Source}");
                    trace.ClassifierCode = confirmed;
                    trace.ClassifierConfidence = resolved.Confidence;
                    trace.ClassifierModel = MultiModelAnalysisService.ClassifierModelTag(clsResult);
                    trace.ClassifierVoteConfirmed = true;
                }
            }
        }

        bild.RecordFrame(run, bild.YoloMs, bild.DinoMs, 0, skipped: structuralOnly is null);

        if (structuralOnly is not null)
        {
            trace.Path = "classifier_only_structural";
            trace.FindingsBuilt = 1;
            var evidence = new EvidenceVector(
                YoloConf: clsResult?.Predictions[0].Confidence ?? 0.0, DinoConf: 0.0, FrameCount: 1);
            var (mSrc, mEst) = MultiModelAnalysisService.GetDedupMeterMetadata(qwenMeterAccepted: false);
            return new MultiModelBildErgebnis(MultiModelBildAusgang.Grundgeruestbefund, meterNoBox,
                new List<EnhancedFinding> { structuralOnly }, evidence, mSrc, mEst);
        }

        trace.Path = "dino_no_boxes";
        trace.DropReason = "dino_no_boxes";
        return MultiModelBildErgebnis.OhneBox(meterNoBox);
    }
}
