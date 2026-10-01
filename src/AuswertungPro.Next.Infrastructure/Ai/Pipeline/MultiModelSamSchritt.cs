using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using Microsoft.Extensions.Logging;
using VsaCodeResolver = AuswertungPro.Next.Infrastructure.Ai.VsaCodeResolver;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// SAM-Schritt der Mehrmodell-Analyse (Segmentierung der DINO-Boxen, Vollstaendigkeit der Masken,
/// Quantifizierung und Befundbau), ausgelagert aus <see cref="MultiModelAnalysisService"/> (AP05b).
/// Eigene Regeln dieses Modells, bewusst nicht mit YOLO/DINO/Qwen gleichgesetzt:
/// VRAM-Mangel ist ein Kapazitaetsfehler (nur Skip-Quote, nie Ausfallserie oder Neustart), jeder
/// andere Aufruffehler ein Transportfehler; ein Nutzerabbruch wird weitergereicht. Eine vom Modell
/// bewusst verworfene Maske (<c>LowScoreBoxes</c>) ist kein technischer Fehler; nur ein
/// darueber hinausgehender Verlust macht das Bild zum Wiederholungsfall
/// (<see cref="MultiModelRunCompleteness.RecordSam"/>). Die Befunde werden trotzdem gebucht.
/// </summary>
internal sealed class MultiModelSamSchritt
{
    private readonly IVisionPipelineClient _client;
    private readonly ILogger _logger;

    public MultiModelSamSchritt(IVisionPipelineClient client, ILogger logger)
    {
        _client = client;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ergebnis des SAM-Schritts. Ist <see cref="Abschluss"/> gesetzt, endet das Bild hier
    /// (Kapazitaets- oder Transportfehler); sonst liegen Antwort, Befunde und Wiederholungsbedarf vor.
    /// </summary>
    internal sealed record Ergebnis(
        MultiModelBildErgebnis? Abschluss,
        SamResponse? Antwort,
        long SamMs,
        bool ErneutNoetig,
        List<EnhancedFinding> Befunde,
        int VorausNichtMetriert)
    {
        public static Ergebnis Beendet(MultiModelBildErgebnis abschluss)
            => new(abschluss, null, 0, false, new List<EnhancedFinding>(), 0);
    }

    /// <summary>Step 3 und 4 des Bildschritts: SAM-Segmentierung und Quantifizierung.</summary>
    public async Task<Ergebnis> SegmentiereAsync(
        MultiModelLaufZustand run, MultiModelBildKontext bild, DinoResponse dinoResult, CancellationToken ct)
    {
        var trace = bild.Trace;
        run.Progress?.Report(new VideoAnalysisProgress(run.FrameIndex, run.TotalFrames,
            $"Frame {run.FrameIndex}/{run.TotalFrames} – SAM Segmentation ({dinoResult.Detections.Count} Boxes)...",
            FramePreviewPng: bild.FrameBytes));

        var samBoxes = dinoResult.Detections
            .Select(d => new SamBoundingBox(d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence))
            .ToList();

        var phaseSw = Stopwatch.StartNew();
        var aufruf = await MultiModelSidecarAufruf.AusfuehrenAsync(() => _client.SegmentSamAsync(
            new SamRequest(bild.FrameBase64, samBoxes, run.PipeDiameterMm > 0 ? run.PipeDiameterMm : null), ct), ct)
            .ConfigureAwait(false);
        if (aufruf.Fehlerart == MultiModelFehlerart.Kapazitaet)
        {
            // Paket 2/A4: VRAM-Mangel = Kapazitaetsfehler, KEIN Transport-Ausfall:
            // kein Outage-Zaehler, kein Neustart — wie ein Modellfehler ueberspringen
            // (Skip-Quote + Incomplete); das Checkpoint-Journal schreibt weiter retry_required.
            var ex = aufruf.Fehler!;
            _logger.LogWarning(ex, "Frame {Frame}: SAM wegen VRAM-Mangels uebersprungen", run.FrameIndex);
            run.Progress?.Report(new VideoAnalysisProgress(run.FrameIndex, run.TotalFrames,
                $"Frame {run.FrameIndex} – SAM übersprungen: {ex.Message}"));
            bild.RecordFrame(run, bild.YoloMs, bild.DinoMs, phaseSw.ElapsedMilliseconds);
            trace.Path = "sam_error";
            trace.DropReason = "vram_insufficient";
            MultiModelAnalysisService.MarkTraceDegraded(trace, "vram_insufficient");
            return Ergebnis.Beendet(MultiModelBildErgebnis.VramMangel(ex.Message, bild.EstimatedMeter));
        }
        if (aufruf.Fehlerart == MultiModelFehlerart.Transport)
        {
            var ex = aufruf.Fehler!;
            _logger.LogWarning(ex, "Frame {Frame}: SAM segmentation failed", run.FrameIndex);
            run.Progress?.Report(new VideoAnalysisProgress(run.FrameIndex, run.TotalFrames,
                $"Frame {run.FrameIndex} – SAM Fehler: {ex.Message}"));
            bild.RecordFrame(run, bild.YoloMs, bild.DinoMs, phaseSw.ElapsedMilliseconds);
            return Ergebnis.Beendet(MultiModelBildErgebnis.Transportfehler("sam_error", bild.EstimatedMeter));
        }
        var samResult = aufruf.Antwort!;
        var samMs = phaseSw.ElapsedMilliseconds;
        trace.SamMaskCount = samResult.Masks.Count;

        var erneutNoetig = RecordSamCompletion(run, samResult, samBoxes.Count, trace);

        // ── Step 4: Quantification ──
        var quantified = MaskQuantificationService.QuantifyAll(samResult, run.PipeDiameterMm);
        var (findings, proximitySuppressedCount) = BuildFindings(run.FrameIndex, samResult, dinoResult, quantified);

        trace.FindingsBuilt = findings.Count;
        trace.CodesFromLabel = findings.Count(f => !string.IsNullOrWhiteSpace(f.VsaCodeHint));
        return new Ergebnis(null, samResult, samMs, erneutNoetig, findings, proximitySuppressedCount);
    }

    /// <summary>
    /// Traegt den SAM-Ausgang in die Vollstaendigkeit des Laufs ein. true = technischer Verlust
    /// (Bild erneut noetig). Bewusst verworfene Masken zaehlen nicht als Verlust; der Trace wird
    /// bei jeder eingeschraenkten Antwort markiert.
    /// </summary>
    private bool RecordSamCompletion(MultiModelLaufZustand run, SamResponse response, int requestedBoxes,
        PipelineFrameTrace trace)
    {
        var failed = run.Completeness.RecordSam(response, requestedBoxes);
        if (response.Degraded || failed)
        {
            _logger.LogWarning("Frame {Frame}: SAM – {Skipped}/{Requested} Boxen nicht segmentiert (Review).",
                run.FrameIndex, response.SkippedBoxes, requestedBoxes);
            run.Progress?.Report(new VideoAnalysisProgress(run.FrameIndex, run.TotalFrames,
                $"Frame {run.FrameIndex} – SAM: {response.SkippedBoxes} Box(en) nicht segmentiert – Review nötig"));
            MultiModelAnalysisService.MarkTraceDegraded(trace, $"sam_skipped_{response.SkippedBoxes}_of_{requestedBoxes}");
        }
        return failed;
    }

    /// <summary>
    /// Befunde aus den quantifizierten Masken ueber SegmentedFinding und Naehe-Gate. Ohne Kalibrierung
    /// im Batch: Fluchtpunkt = Bildmitte, Rohrradius-Fallback 0.5. Liefert die Befunde und die Zahl
    /// der erkannten, aber als «voraus» nicht metrierten Masken.
    /// </summary>
    private (List<EnhancedFinding> Findings, int ProximitySuppressed) BuildFindings(
        int frameIndex, SamResponse samResult, DinoResponse dinoResult, IReadOnlyList<MaskQuantificationService.QuantifiedMask> quantified)
    {
        var segmented = SegmentedFindingBuilder.Build(
            samResult, dinoResult.Detections, quantified,
            vanishX: 0.5, vanishY: 0.5, pipeRadiusNorm: 0.5,
            AuswertungPro.Next.Application.Ai.MetrierungProximityThresholds.Default);

        int proximitySuppressedCount = 0;
        var findings = new List<EnhancedFinding>(segmented.Count);
        foreach (var seg in segmented)
        {
            var q = seg.Quant;
            if (string.IsNullOrWhiteSpace(q.Label))
                continue;
            if (!seg.Proximity.IsCodierbar)
            {
                proximitySuppressedCount++;   // ahead_of_camera: erkannt, aber nicht metriert
                continue;
            }

            var bbox = MultiModelFrameAnalysisMapper.GetNormalizedBbox(
                seg.Mask,
                samResult.ImageWidth,
                samResult.ImageHeight);
            findings.Add(new EnhancedFinding(
                Label: q.Label,
                VsaCodeHint: VsaCodeResolver.InferCodeFromLabel(q.Label),
                Severity: QuantificationSeverityPolicy.Estimate(
                    q.CrossSectionReductionPercent,
                    q.IntrusionPercent,
                    q.HeightMm,
                    q.ExtentPercent),
                PositionClock: VsaCodeResolver.NormalizeClock(q.ClockPosition),
                ExtentPercent: q.ExtentPercent,
                HeightMm: q.HeightMm,
                WidthMm: q.WidthMm,
                IntrusionPercent: q.IntrusionPercent,
                CrossSectionReductionPercent: q.CrossSectionReductionPercent,
                DiameterReductionMm: null,
                BboxX1: bbox.X1,
                BboxY1: bbox.Y1,
                BboxX2: bbox.X2,
                BboxY2: bbox.Y2,
                Notes: $"DINO conf={(seg.Dino?.Confidence ?? q.Confidence):F2}"
            ));
        }
        if (proximitySuppressedCount > 0)
            _logger.LogDebug("Frame {Frame}: {Count} Befund(e) als 'ahead_of_camera' nicht metriert.",
                frameIndex, proximitySuppressedCount);
        return (findings, proximitySuppressedCount);
    }
}
