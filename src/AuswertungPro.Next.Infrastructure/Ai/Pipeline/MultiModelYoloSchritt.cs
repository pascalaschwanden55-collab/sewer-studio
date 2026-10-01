using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// YOLO-Schritt der Mehrmodell-Analyse (Vorpruefung mit YOLO-Detect, Umgehung fuer Bestandsaufnahme
/// und gesperrten Detektor, Qualifikationsentzug, Klassenschwellen, COCO-Warnung und das als
/// irrelevant uebersprungene Bild), ausgelagert aus <see cref="MultiModelAnalysisService"/> (AP05b).
/// Eigene Regeln dieses Modells, bewusst nicht mit DINO/SAM/Qwen gleichgesetzt:
/// YOLO filtert nur mit einer Antwort, die ausdruecklich <c>qualified=true</c> traegt; fehlt sie
/// (auch null), ist der Detektor fuer den Rest des Laufs gesperrt und DINO/SAM laufen ohne Filter.
/// VRAM-Mangel ist ein Kapazitaetsfehler, jeder andere Fehler im Aufruf samt Nachfilterung ein
/// Transportfehler, ein Nutzerabbruch wird weitergereicht.
/// </summary>
internal sealed class MultiModelYoloSchritt
{
    private readonly IVisionPipelineClient _client;
    private readonly PipelineConfig _config;
    private readonly double _minClassConfidence;
    private readonly string _expectedYoloModel;
    private readonly ILogger _logger;

    public MultiModelYoloSchritt(IVisionPipelineClient client, PipelineConfig config, double minClassConfidence,
        string expectedYoloModel, ILogger logger)
    {
        _client = client;
        _config = config;
        _minClassConfidence = minClassConfidence;
        _expectedYoloModel = expectedYoloModel;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Warum YOLO-Detect fuer ein Bild nicht laeuft. YOLO erkennt nur Schaeden — Bestandsaufnahme
    /// (Anschluesse, Boegen, Ablagerungen, Rohranfang/Ende) wuerde verpasst; deshalb laufen jedes
    /// dritte Bild und die BCD-/BCE-Zonen immer an DINO weiter, ebenso jedes Bild bei gesperrtem Detektor.
    /// </summary>
    internal readonly record struct Umgehung(bool QualifikationGesperrt, bool BcdZone, bool BceZone, bool Sweep)
    {
        public bool Aktiv => QualifikationGesperrt || BcdZone || BceZone || Sweep;

        public static Umgehung Bestimme(MultiModelLaufZustand run, double t, double estimatedMeter, double frameStepSeconds)
        {
            bool isAfterOsd = t > 20.0; // OSD-Einblendung 10-20 Sekunden je nach Operateur
            bool isBcdZone = isAfterOsd && estimatedMeter < 1.5 && run.FrameIndex <= 10;
            bool isBceZone = run.Duration > 10 && t > (run.Duration - frameStepSeconds * 2);
            // Jeden 3. Frame immer analysieren (Bestandsaufnahme-Sweep)
            bool isPeriodicSweep = isAfterOsd && (run.FrameIndex % 3 == 0);
            return new Umgehung(!run.DetectorQualified, isBcdZone, isBceZone, isPeriodicSweep);
        }
    }

    /// <summary>
    /// Ist <see cref="Abschluss"/> gesetzt, endet das Bild hier (Fehler oder irrelevant); sonst geht
    /// <see cref="Antwort"/> weiter. <see cref="QualifikationGesperrt"/> kann durch die Antwort erst
    /// entstehen und entscheidet, ob YOLO spaeter als Beleg zaehlt.
    /// </summary>
    internal sealed record Ergebnis(MultiModelBildErgebnis? Abschluss, YoloResponse? Antwort, bool QualifikationGesperrt);

    /// <summary>Step 1 des Bildschritts: YOLO Pre-Screening. Setzt <see cref="MultiModelBildKontext.YoloMs"/>.</summary>
    public async Task<Ergebnis> PruefeAsync(
        MultiModelLaufZustand run, MultiModelBildKontext bild, Umgehung umgehung, CancellationToken ct)
    {
        var trace = bild.Trace;
        var progress = run.Progress;
        var totalFrames = run.TotalFrames;
        bool detectorQualificationBypass = umgehung.QualifikationGesperrt;
        var phaseSw = Stopwatch.StartNew();
        YoloResponse yoloResult;
        long yoloMs;

        if (umgehung.Aktiv)
        {
            // YOLO-Detect ueberspringen — Frame direkt an DINO/Qwen weiterleiten.
            // frame_class ehrlich als "sweep" markieren: BCD/BCE sind hier nur
            // Zonen-Heuristiken, keine Detektionen.
            yoloResult = new YoloResponse(
                IsRelevant: true,
                Detections: Array.Empty<YoloDetectionDto>(),
                FrameClass: detectorQualificationBypass ? "detector_unqualified" : "sweep",
                InferenceTimeMs: 0);
            yoloMs = 0;
            var zone = detectorQualificationBypass ? "YOLO gesperrt – DINO/SAM-Prüfung"
                : umgehung.BcdZone ? "BCD-Zone (Rohranfang)"
                : umgehung.BceZone ? "BCE-Zone (Rohrende)"
                : "Bestandsaufnahme-Sweep";
            _logger.LogDebug("Frame {Frame}: Telemetrie-Bypass ({Zone}) @ {Meter:F2}m",
                run.FrameIndex, zone, bild.EstimatedMeter);
            progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                $"Frame {run.FrameIndex}/{totalFrames} – {zone} @ {bild.EstimatedMeter:F1}m",
                FramePreviewPng: bild.FrameBytes));
        }
        else
        {
            progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                $"Frame {run.FrameIndex}/{totalFrames} – YOLO Pre-Screening...",
                FramePreviewPng: bild.FrameBytes));

            // Aufruf samt Nachfilterung liegt in derselben Fehlereinordnung wie vorher im try.
            var aufruf = await MultiModelSidecarAufruf.AusfuehrenAsync(async () =>
            {
                // Niedrigsten klassenspezifischen Threshold senden (mehr Kandidaten),
                // dann in C# pro Klasse nachfiltern
                double minConf = _minClassConfidence;
                var antwort = await _client.DetectYoloAsync(
                    new YoloRequest(bild.FrameBase64, minConf), ct).ConfigureAwait(false);

                // Die Qualifikation kann sich zwischen /health und Inferenz aendern.
                // Auch die konkrete Antwort muss deshalb ein ausdrueckliches true tragen.
                if (antwort.DetectorQualified != true)
                {
                    run.EffectiveDetectorQualified = antwort.DetectorQualified;
                    run.DetectorQualified = false;
                    run.DetectorQualificationReason =
                        antwort.DetectorQualificationReason
                        ?? "YOLO-Antwort ohne positive Detektorqualifikation";
                    detectorQualificationBypass = true;
                    trace.YoloBypass = true;
                    MultiModelAnalysisService.MarkTraceDegraded(trace, "detector_unqualified_response");
                    antwort = antwort with
                    {
                        IsRelevant = true,
                        Detections = Array.Empty<YoloDetectionDto>(),
                        FrameClass = "detector_unqualified",
                    };
                    progress?.Report(new VideoAnalysisProgress(
                        run.FrameIndex,
                        totalFrames,
                        "WARNUNG: YOLO-Freigabe während des Laufs fehlt – DINO/SAM laufen weiter."));
                }

                WarnOnCocoFallback(run, antwort, detectorQualificationBypass);
                return ApplyClassThresholds(antwort, detectorQualificationBypass);
            }, ct).ConfigureAwait(false);
            if (aufruf.Fehlerart == MultiModelFehlerart.Kapazitaet)
            {
                // Paket 2/A4: VRAM-Mangel ist ein Kapazitaetsfehler, KEIN Transport-Ausfall:
                // kein Outage-Zaehler, kein Neustart — wie ein Modellfehler ueberspringen
                // (Skip-Quote + Incomplete); das Checkpoint-Journal schreibt weiter retry_required.
                var ex = aufruf.Fehler!;
                _logger.LogWarning(ex, "Frame {Frame}: YOLO wegen VRAM-Mangels uebersprungen", run.FrameIndex);
                progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                    $"Frame {run.FrameIndex} – YOLO übersprungen: {ex.Message}"));
                bild.RecordFrame(run, phaseSw.ElapsedMilliseconds, 0, 0);
                trace.Path = "yolo_error";
                trace.DropReason = "vram_insufficient";
                MultiModelAnalysisService.MarkTraceDegraded(trace, "vram_insufficient");
                return new Ergebnis(MultiModelBildErgebnis.VramMangel(ex.Message, bild.EstimatedMeter), null,
                    detectorQualificationBypass);
            }
            if (aufruf.Fehlerart == MultiModelFehlerart.Transport)
            {
                var ex = aufruf.Fehler!;
                _logger.LogWarning(ex, "Frame {Frame}: YOLO detection failed", run.FrameIndex);
                progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                    $"Frame {run.FrameIndex} – YOLO Fehler: {ex.Message}"));
                bild.RecordFrame(run, phaseSw.ElapsedMilliseconds, 0, 0);
                return new Ergebnis(MultiModelBildErgebnis.Transportfehler("yolo_error", bild.EstimatedMeter), null,
                    detectorQualificationBypass);
            }
            yoloResult = aufruf.Antwort!;
            yoloMs = phaseSw.ElapsedMilliseconds;
        }
        bild.YoloMs = yoloMs;

        trace.YoloRelevant = yoloResult.IsRelevant;
        trace.YoloDetectionCount = yoloResult.Detections.Count;

        if (!yoloResult.IsRelevant)
        {
            run.SkippedFrames++;
            progress?.Report(new VideoAnalysisProgress(run.FrameIndex, totalFrames,
                $"Frame {run.FrameIndex}/{totalFrames} – übersprungen (YOLO: irrelevant, {run.SkippedFrames} gesamt)"));
            bild.RecordFrame(run, yoloMs, 0, 0);
            trace.Path = "yolo_irrelevant";
            trace.DropReason = "yolo_irrelevant";
            return new Ergebnis(MultiModelBildErgebnis.Uebersprungen(bild.EstimatedMeter), yoloResult,
                detectorQualificationBypass);
        }

        return new Ergebnis(null, yoloResult, detectorQualificationBypass);
    }

    /// <summary>
    /// COCO-Fallback sichtbar machen: laeuft der Sidecar nicht mit den eigenen Gewichten (yolo26m),
    /// ist die Schadenserkennung faktisch blind — das darf nie wieder still passieren (realer Vorfall
    /// 2026-06-09). Einmal je Lauf; bei gesperrtem Detektor ohne Bedeutung.
    /// </summary>
    private void WarnOnCocoFallback(MultiModelLaufZustand run, YoloResponse yoloResult, bool detectorQualificationBypass)
    {
        if (!detectorQualificationBypass
            && !run.YoloFallbackWarned
            && yoloResult.ModelName is { Length: > 0 } yoloModelName
            && !yoloModelName.Contains(_expectedYoloModel, StringComparison.OrdinalIgnoreCase))
        {
            run.YoloFallbackWarned = true;
            _logger.LogWarning(
                "YOLO laeuft mit '{Model}' statt der eigenen Gewichte ({Expected}) – COCO-Fallback, Schadenserkennung stark eingeschraenkt!",
                yoloModelName, _expectedYoloModel);
            run.Progress?.Report(new VideoAnalysisProgress(run.FrameIndex, run.TotalFrames,
                $"WARNUNG: YOLO-Fallback aktiv ('{yoloModelName}' statt {_expectedYoloModel}) – Schadenserkennung eingeschränkt!"));
        }
    }

    /// <summary>
    /// Klassenspezifische Filterung: Jede Klasse hat ihren eigenen Schwellenwert. Nur mit
    /// qualifiziertem Detektor; ohne Treffer ist das Bild danach irrelevant.
    /// </summary>
    private YoloResponse ApplyClassThresholds(YoloResponse yoloResult, bool detectorQualificationBypass)
    {
        if (detectorQualificationBypass
            || yoloResult.Detections.Count == 0
            || _config.YoloClassConfidence.Count == 0)
            return yoloResult;

        var filtered = yoloResult.Detections
            .Where(d =>
            {
                // VSA-Hauptcode aus YOLO-Klassenname ableiten ("crack" → BAB,
                // legacy "BAB_crack" → BAB); ohne Zuordnung gilt die Default-Schwelle
                var baseCode = YoloClassVsaMapper.ToVsaMainCode(d.ClassName);
                var threshold = baseCode is not null
                    ? _config.YoloClassConfidence.GetValueOrDefault(baseCode, _config.YoloConfidence)
                    : _config.YoloConfidence;
                return d.Confidence >= threshold;
            })
            .ToList();
        return yoloResult with
        {
            Detections = filtered,
            IsRelevant = filtered.Count > 0
        };
    }
}
