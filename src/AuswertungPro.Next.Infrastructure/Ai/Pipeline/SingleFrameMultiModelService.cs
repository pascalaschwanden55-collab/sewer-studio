using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Orchestriert YOLO → Florence-2 → SAM 2 fuer einen einzelnen Frame.
/// Extrahiert aus MultiModelAnalysisService, ohne Video-Streaming und Temporal-Dedup.
/// Fuer den Codiermodus: "Jetzt analysieren" auf dem aktuellen Frame.
/// </summary>
public sealed class SingleFrameMultiModelService
{
    private readonly VisionPipelineClient _client;
    private readonly double _yoloConfidence;
    private readonly double _dinoBoxThreshold;
    private readonly double _dinoTextThreshold;

    /// <summary>
    /// Recall-Boost: Wenn YOLO einen Frame als irrelevant markiert, optional trotzdem DINO pruefen.
    /// </summary>
    public bool RunDinoFallbackOnIrrelevantFrames { get; set; } = true;

    /// <summary>Min Confidence fuer Rohrkreis-Erkennung im Tiefenfilter.</summary>
    private const double MinPipeAxisConfidence = 0.4;

    public SingleFrameMultiModelService(
        VisionPipelineClient client,
        double yoloConfidence = 0.25,
        // Audit 2026-05-15: DINO-Schwellen gesenkt (box 0.30→0.20, text 0.25→0.15).
        // Vorher fand DINO bei Kanalbildern fast nichts; mehr Recall jetzt, die
        // false-positives werden vom Konsens-Filter (≥2 Modelle) + QualityGate
        // abgefangen.
        double dinoBoxThreshold = 0.20,
        double dinoTextThreshold = 0.15)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _yoloConfidence = yoloConfidence;
        _dinoBoxThreshold = dinoBoxThreshold;
        _dinoTextThreshold = dinoTextThreshold;
    }

    /// <summary>
    /// Analysiert einen einzelnen Frame mit der Multi-Model Pipeline.
    /// </summary>
    /// <param name="pngBytes">Frame als PNG-Bytes.</param>
    /// <param name="pipeDiameterMm">Rohr-Nenndurchmesser in mm (aus Haltung).</param>
    /// <param name="calibration">Optionale Kalibrierung fuer praezisere Messungen.</param>
    /// <param name="ct">CancellationToken.</param>
    public async Task<SingleFrameResult> AnalyzeFrameAsync(
        byte[] pngBytes,
        int pipeDiameterMm,
        PipeCalibration? calibration = null,
        CancellationToken ct = default)
    {
        if (pngBytes == null || pngBytes.Length == 0)
            return SingleFrameResult.Empty("Kein Frame-Bild");

        var b64 = Convert.ToBase64String(pngBytes);
        double yoloMs = 0, dinoMs = 0, samMs = 0;

        // Bildgroesse aus PNG-Header lesen
        var (imgWidth, imgHeight) = ReadPngDimensions(pngBytes);

        // Rohrkreis fuer Tiefenfilter ermitteln (pipe_axis, ~5ms)
        PipeAxisResult? pipeAxis = null;
        try
        {
            pipeAxis = await _client.AnalyzePipeAxisAsync(
                new PipeAxisRequest(b64, pipeDiameterMm > 0 ? pipeDiameterMm : null), ct);
        }
        catch { /* Sidecar nicht verfuegbar → weiter ohne Rohrkreis */ }

        // Aufnahmetechnik-Klassifikation (~0.1ms) — ViewType als Info mitgeben
        string? detectedViewType = null;
        try
        {
            var vtResp = await _client.ClassifyViewTypeAsync(new ViewTypeRequest(b64), ct);
            var vt = vtResp.Prediction;
            if (vt.Confidence > 0.7)
                detectedViewType = vt.ViewType;
        }
        catch { /* ViewType-Modell nicht verfuegbar → weiter ohne */ }

        try
        {
            // 1. YOLO Pre-Screening
            var yoloReq = new YoloRequest(b64, _yoloConfidence);
            var yoloResp = await _client.DetectYoloAsync(yoloReq, ct);
            yoloMs = yoloResp.InferenceTimeMs;
            RecordYoloRaw(yoloResp, yoloMs, imgWidth, imgHeight, detectedViewType);

            if (!yoloResp.IsRelevant && !RunDinoFallbackOnIrrelevantFrames)
            {
                return new SingleFrameResult(
                    IsRelevant: false,
                    DinoDetections: Array.Empty<DinoDetectionDto>(),
                    SamResponse: null,
                    QuantifiedMasks: Array.Empty<MaskQuantificationService.QuantifiedMask>(),
                    YoloTimeMs: yoloMs, DinoTimeMs: 0, SamTimeMs: 0,
                    Error: null)
                { ViewType = detectedViewType };
            }

            // 2. YOLO-Detektionen als Boxen nutzen (DINO versagt bei Kanalbildern)
            // Tiefenfilter: Nur Boxen im Nahbereich durchlassen (nicht in der Rohrtiefe)
            var yoloAsDetections = yoloResp.Detections
                .Where(d => SamPromptCodeFilter.ShouldUseAsSamPrompt(d.ClassName))
                .Where(d => !IsNearCenter(d.X1, d.Y1, d.X2, d.Y2, imgWidth, imgHeight))
                .Select(d => new DinoDetectionDto(
                    d.X1, d.Y1, d.X2, d.Y2,
                    Label: d.ClassName,
                    Confidence: d.Confidence,
                    Phrase: d.ClassName))
                .ToList();

            // Optional: DINO als Ergaenzung (wenn es mal etwas findet)
            IReadOnlyList<DinoDetectionDto> nearDetections = yoloAsDetections;
            try
            {
                var dinoReq = new DinoRequest(b64, null, _dinoBoxThreshold, _dinoTextThreshold);
                var dinoResp = await _client.DetectDinoAsync(dinoReq, ct);
                dinoMs = dinoResp.InferenceTimeMs;
                RecordDinoRaw(dinoResp, dinoMs, yoloAsDetections.Count, detectedViewType);
                if (dinoResp.Detections.Count > 0)
                {
                    // DINO hat etwas gefunden — DINO-Boxen bevorzugen (praeziser)
                    nearDetections = dinoResp.Detections
                        .Where(d => SamPromptCodeFilter.ShouldUseAsSamPrompt(d.Label))
                        .Where(d => !IsInsidePipeCircle(d.X1, d.Y1, d.X2, d.Y2, pipeAxis, imgWidth, imgHeight))
                        .ToList();
                    // Wenn DINO-Filter alles wegfiltert, YOLO-Boxen als Fallback
                    if (nearDetections.Count == 0)
                        nearDetections = yoloAsDetections;
                }
            }
            catch { /* DINO optional — wenn es fehlt, reichen YOLO-Boxen */ }

            // 3. SAM 2 Segmentation. Wenn YOLO/DINO keine Box liefert, scannt SAM
            // den Rohrwand-Annulus sichtbar ab statt still ohne Segmentierung
            // zurueckzukehren.
            var samBoxes = nearDetections
                .Select(d => new SamBoundingBox(d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence))
                .ToList();

            var ringScan = samBoxes.Count == 0
                ? PipeAxisRingScanFactory.Create(pipeAxis, imgWidth, imgHeight)
                : null;

            if (samBoxes.Count == 0 && ringScan is null)
            {
                RecordMultiModelRaw(
                    summary: "SAM nicht gestartet: keine Boxen und kein Ring-Scan moeglich",
                    latencyMs: dinoMs,
                    rawOutput: null,
                    metadata: new Dictionary<string, string>
                    {
                        ["view_type"] = detectedViewType ?? "",
                        ["yolo_boxes_after_filter"] = yoloAsDetections.Count.ToString(CultureInfo.InvariantCulture),
                        ["dino_boxes_after_filter"] = nearDetections.Count.ToString(CultureInfo.InvariantCulture),
                        ["ring_scan"] = "false"
                    });

                return new SingleFrameResult(
                    IsRelevant: yoloResp.IsRelevant,
                    DinoDetections: Array.Empty<DinoDetectionDto>(),
                    SamResponse: null,
                    QuantifiedMasks: Array.Empty<MaskQuantificationService.QuantifiedMask>(),
                    YoloTimeMs: yoloMs, DinoTimeMs: dinoMs, SamTimeMs: 0,
                    Error: null)
                { ViewType = detectedViewType };
            }

            var samReq = new SamRequest(
                b64,
                samBoxes,
                PipeDiameterMm: pipeDiameterMm > 0 ? pipeDiameterMm : null,
                RingScan: ringScan);
            var samResp = await _client.SegmentSamAsync(samReq, ct);
            samMs = samResp.InferenceTimeMs;
            RecordSamRaw(samResp, samMs, samBoxes.Count, ringScan is not null, detectedViewType);

            // 4. Quantifizierung: Pixel-Masken → mm, %, Uhrposition
            var quantified = new List<MaskQuantificationService.QuantifiedMask>();
            foreach (var mask in samResp.Masks)
            {
                var q = calibration != null
                    ? MaskQuantificationService.Quantify(mask, samResp.ImageWidth, samResp.ImageHeight, pipeDiameterMm, calibration)
                    : MaskQuantificationService.Quantify(mask, samResp.ImageWidth, samResp.ImageHeight, pipeDiameterMm);
                quantified.Add(q);
            }

            return new SingleFrameResult(
                IsRelevant: yoloResp.IsRelevant || nearDetections.Count > 0,
                DinoDetections: nearDetections,
                SamResponse: samResp,
                QuantifiedMasks: quantified,
                YoloTimeMs: yoloMs, DinoTimeMs: dinoMs, SamTimeMs: samMs,
                Error: null)
            { ViewType = detectedViewType };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
            {
                Stage = AiDiagnosticStage.YoloError,
                Source = nameof(SingleFrameMultiModelService),
                Summary = $"SingleFrame Multi-Model Fehler: {ex.Message}"
            });
            return SingleFrameResult.Empty($"Multi-Model Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Tiefenfilter: Prueft ob eine DINO-Box komplett INNERHALB des Rohrkreises liegt.
    /// Wenn ja → Box zeigt etwas in der Rohrtiefe, nicht an der Rohrwand → verwerfen.
    /// Echte Schaeden beruehren die Rohrwand = mindestens eine Ecke liegt AUSSERHALB oder
    /// AM RAND des Rohrkreises.
    /// Nutzt den erkannten Rohrkreis (PipeCenter + PipeRadius) vom pipe_axis-Endpoint.
    /// Fallback wenn kein pipe_axis: Bildmitte + 35% Radius.
    /// </summary>
    private static bool IsInsidePipeCircle(
        double x1, double y1, double x2, double y2,
        PipeAxisResult? pipeAxis, int imgW, int imgH)
    {
        if (imgW <= 0 || imgH <= 0) return false;

        // Rohrkreis: Mittelpunkt und Radius (normalisiert)
        double cx, cy, rx, ry;
        if (pipeAxis != null && pipeAxis.Confidence >= MinPipeAxisConfidence)
        {
            // Vom Sidecar erkannter Rohrkreis
            cx = pipeAxis.PipeCenterX / imgW;
            cy = pipeAxis.PipeCenterY / imgH;
            rx = pipeAxis.PipeRadiusX / imgW;
            ry = pipeAxis.PipeRadiusY / imgH;
        }
        else
        {
            // Fallback: Bildmitte, 35% Radius
            cx = 0.5;
            cy = 0.5;
            rx = 0.35;
            ry = 0.35;
        }

        // Innerer Rohrkreis = 50% des vollen Radius (Tiefenzone)
        // Alles innerhalb von 50% des Rohrradius ist "in der Tiefe"
        // Zwischen 50-100% ist der Nahbereich (Rohrwand, echte Schaeden)
        double innerRx = rx * 0.50;
        double innerRy = ry * 0.50;

        // Pruefe ob ALLE 4 Ecken der Box innerhalb der Tiefen-Ellipse liegen
        bool AllCornersInside()
        {
            double[] xs = { x1 / imgW, x2 / imgW };
            double[] ys = { y1 / imgH, y2 / imgH };

            foreach (var bx in xs)
            foreach (var by in ys)
            {
                double dx = (bx - cx) / innerRx;
                double dy = (by - cy) / innerRy;
                if (dx * dx + dy * dy > 1.0)
                    return false; // Ecke liegt ausserhalb → Box ist nah
            }
            return true; // Alle Ecken innerhalb → Box ist in der Tiefe
        }

        return AllCornersInside();
    }

    /// <summary>
    /// Tiefenfilter fuer YOLO-Boxen: Prueft ob die Box im Zentrum des Bildes liegt
    /// UND klein ist = in der Rohrtiefe = nicht codierbar.
    /// Nur Boxen die am Bildrand liegen (Rohrwand, nah) werden durchgelassen.
    /// </summary>
    private static bool IsNearCenter(double x1, double y1, double x2, double y2, int imgW, int imgH)
    {
        if (imgW <= 0 || imgH <= 0) return false;

        // Normalisierte Box
        double nx1 = x1 / imgW, ny1 = y1 / imgH;
        double nx2 = x2 / imgW, ny2 = y2 / imgH;
        double ncx = (nx1 + nx2) / 2.0;
        double ncy = (ny1 + ny2) / 2.0;
        double nArea = (nx2 - nx1) * (ny2 - ny1);

        // Box ist "in der Tiefe" wenn:
        // 1. Mittelpunkt im inneren 40% des Bildes (30%-70% horizontal und vertikal)
        // 2. Box-Flaeche < 15% des Bildes (kleine Boxen = weit weg)
        bool centerInCore = ncx > 0.30 && ncx < 0.70 && ncy > 0.30 && ncy < 0.70;
        bool smallBox = nArea < 0.15;

        return centerInCore && smallBox;
    }

    /// <summary>
    /// Liest Bildbreite und -hoehe aus dem PNG-Header (IHDR-Chunk, Bytes 16-23).
    /// </summary>
    private static (int Width, int Height) ReadPngDimensions(byte[] pngBytes)
    {
        // PNG-Header: 8 Bytes Signatur, dann IHDR-Chunk mit Breite (4 Bytes BE) und Hoehe (4 Bytes BE)
        if (pngBytes.Length < 24) return (720, 576);
        try
        {
            int w = (pngBytes[16] << 24) | (pngBytes[17] << 16) | (pngBytes[18] << 8) | pngBytes[19];
            int h = (pngBytes[20] << 24) | (pngBytes[21] << 16) | (pngBytes[22] << 8) | pngBytes[23];
            return (w > 0 && h > 0) ? (w, h) : (720, 576);
        }
        catch
        {
            return (720, 576);
        }
    }

    private static void RecordYoloRaw(
        YoloResponse response,
        double latencyMs,
        int imgWidth,
        int imgHeight,
        string? viewType)
    {
        AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.YoloRaw,
            Source = nameof(SingleFrameMultiModelService),
            Summary = $"YOLO relevant={response.IsRelevant}, boxes={response.Detections.Count}, class={response.FrameClass ?? "-"}",
            RawOutput = FormatYoloDetections(response.Detections),
            LatencyMs = latencyMs,
            Metadata = new Dictionary<string, string>
            {
                ["view_type"] = viewType ?? "",
                ["image_size"] = $"{imgWidth}x{imgHeight}",
                ["boxes"] = response.Detections.Count.ToString(CultureInfo.InvariantCulture),
                ["frame_class"] = response.FrameClass ?? "",
                ["is_relevant"] = response.IsRelevant.ToString()
            }
        });
    }

    private static void RecordDinoRaw(
        DinoResponse response,
        double latencyMs,
        int yoloBoxesAfterFilter,
        string? viewType)
    {
        RecordMultiModelRaw(
            summary: $"DINO raw boxes={response.Detections.Count}, yolo_fallback_boxes={yoloBoxesAfterFilter}",
            latencyMs: latencyMs,
            rawOutput: FormatDinoDetections(response.Detections),
            metadata: new Dictionary<string, string>
            {
                ["stage"] = "dino",
                ["view_type"] = viewType ?? "",
                ["dino_boxes_raw"] = response.Detections.Count.ToString(CultureInfo.InvariantCulture),
                ["yolo_boxes_after_filter"] = yoloBoxesAfterFilter.ToString(CultureInfo.InvariantCulture)
            });
    }

    private static void RecordSamRaw(
        SamResponse response,
        double latencyMs,
        int promptBoxes,
        bool usedRingScan,
        string? viewType)
    {
        RecordMultiModelRaw(
            summary: $"SAM masks={response.Masks.Count}, boxes={promptBoxes}, ring_scan={usedRingScan}",
            latencyMs: latencyMs,
            rawOutput: FormatSamMasks(response.Masks),
            metadata: new Dictionary<string, string>
            {
                ["stage"] = "sam",
                ["view_type"] = viewType ?? "",
                ["prompt_boxes"] = promptBoxes.ToString(CultureInfo.InvariantCulture),
                ["masks"] = response.Masks.Count.ToString(CultureInfo.InvariantCulture),
                ["ring_scan"] = usedRingScan.ToString(),
                ["image_size"] = $"{response.ImageWidth}x{response.ImageHeight}"
            });
    }

    private static void RecordMultiModelRaw(
        string summary,
        double latencyMs,
        string? rawOutput,
        IReadOnlyDictionary<string, string> metadata)
    {
        AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.MultiModelRaw,
            Source = nameof(SingleFrameMultiModelService),
            Summary = summary,
            RawOutput = rawOutput,
            LatencyMs = latencyMs,
            Metadata = metadata
        });
    }

    private static string FormatYoloDetections(IReadOnlyList<YoloDetectionDto> detections)
        => string.Join(Environment.NewLine, detections.Select(d =>
            $"{d.ClassName} conf={d.Confidence:F2} box=[{d.X1:F0},{d.Y1:F0},{d.X2:F0},{d.Y2:F0}]"));

    private static string FormatDinoDetections(IReadOnlyList<DinoDetectionDto> detections)
        => string.Join(Environment.NewLine, detections.Select(d =>
            $"{d.Label} conf={d.Confidence:F2} phrase={d.Phrase ?? ""} box=[{d.X1:F0},{d.Y1:F0},{d.X2:F0},{d.Y2:F0}]"));

    private static string FormatSamMasks(IReadOnlyList<SamMaskResult> masks)
        => string.Join(Environment.NewLine, masks.Select(m =>
            $"{m.Label} conf={m.Confidence:F2} area={m.MaskAreaPixels} centroid=({m.CentroidX:F0},{m.CentroidY:F0})"));
}

/// <summary>
/// Ergebnis der Einzelframe Multi-Model Analyse.
/// </summary>
public sealed record SingleFrameResult(
    bool IsRelevant,
    IReadOnlyList<DinoDetectionDto> DinoDetections,
    SamResponse? SamResponse,
    IReadOnlyList<MaskQuantificationService.QuantifiedMask> QuantifiedMasks,
    double YoloTimeMs,
    double DinoTimeMs,
    double SamTimeMs,
    string? Error)
{
    public bool HasDetections => DinoDetections.Count > 0;
    public bool HasMasks => SamResponse?.Masks.Count > 0;
    public double TotalTimeMs => YoloTimeMs + DinoTimeMs + SamTimeMs;

    /// <summary>Erkannter Aufnahmetyp: "axial", "schwenk", "unklar". Null = nicht geprueft.</summary>
    public string? ViewType { get; init; }

    public static SingleFrameResult Empty(string? error = null) => new(
        false, Array.Empty<DinoDetectionDto>(), null,
        Array.Empty<MaskQuantificationService.QuantifiedMask>(),
        0, 0, 0, error);
}
