using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Orchestriert YOLO → DINO → SAM fuer einen einzelnen Frame.
/// Extrahiert aus MultiModelAnalysisService, ohne Video-Streaming und Temporal-Dedup.
/// Fuer den Codiermodus: "Jetzt analysieren" auf dem aktuellen Frame.
/// </summary>
public sealed class SingleFrameMultiModelService
{
    private readonly VisionPipelineClient _client;
    private readonly double _yoloConfidence;
    private readonly double _dinoBoxThreshold;
    private readonly double _dinoTextThreshold;

    public SingleFrameMultiModelService(
        VisionPipelineClient client,
        double yoloConfidence = 0.25,
        double dinoBoxThreshold = 0.30,
        double dinoTextThreshold = 0.25)
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

        try
        {
            // 1. YOLO Pre-Screening
            var yoloReq = new YoloRequest(b64, _yoloConfidence);
            var yoloResp = await _client.DetectYoloAsync(yoloReq, ct);
            yoloMs = yoloResp.InferenceTimeMs;
            // D2-A: echte YOLO-Confidence (hoechste Box) ans QualityGate weiterreichen,
            // statt sie zu verwerfen. So zeigen klar erkannte Befunde wieder hohe Confidence.
            double? yoloMax = yoloResp.Detections.Count > 0
                ? yoloResp.Detections.Max(d => d.Confidence)
                : (double?)null;

            if (!yoloResp.IsRelevant)
            {
                return new SingleFrameResult(
                    IsRelevant: false,
                    DinoDetections: Array.Empty<DinoDetectionDto>(),
                    SamResponse: null,
                    QuantifiedMasks: Array.Empty<MaskQuantificationService.QuantifiedMask>(),
                    YoloTimeMs: yoloMs, DinoTimeMs: 0, SamTimeMs: 0,
                    Error: null, YoloMaxConfidence: yoloMax);
            }

            // 2. DINO Open-Vocabulary Detection
            var dinoReq = new DinoRequest(b64, null, _dinoBoxThreshold, _dinoTextThreshold);
            var dinoResp = await _client.DetectDinoAsync(dinoReq, ct);
            dinoMs = dinoResp.InferenceTimeMs;

            if (dinoResp.Detections.Count == 0)
            {
                return new SingleFrameResult(
                    IsRelevant: true,
                    DinoDetections: Array.Empty<DinoDetectionDto>(),
                    SamResponse: null,
                    QuantifiedMasks: Array.Empty<MaskQuantificationService.QuantifiedMask>(),
                    YoloTimeMs: yoloMs, DinoTimeMs: dinoMs, SamTimeMs: 0,
                    Error: null, YoloMaxConfidence: yoloMax);
            }

            // 3. SAM Segmentation (DINO-Boxes als Input)
            var samBoxes = dinoResp.Detections.Select(d => new SamBoundingBox(
                d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence)).ToList();

            var samReq = new SamRequest(b64, samBoxes, pipeDiameterMm > 0 ? pipeDiameterMm : null);
            var samResp = await _client.SegmentSamAsync(samReq, ct);
            samMs = samResp.InferenceTimeMs;

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
                IsRelevant: true,
                DinoDetections: dinoResp.Detections,
                SamResponse: samResp,
                QuantifiedMasks: quantified,
                YoloTimeMs: yoloMs, DinoTimeMs: dinoMs, SamTimeMs: samMs,
                Error: null, YoloMaxConfidence: yoloMax);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return SingleFrameResult.Empty($"Multi-Model Fehler: {ex.Message}");
        }
    }
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
    string? Error,
    double? YoloMaxConfidence = null)
{
    public bool HasDetections => DinoDetections.Count > 0;
    public bool HasMasks => SamResponse?.Masks.Count > 0;
    public double TotalTimeMs => YoloTimeMs + DinoTimeMs + SamTimeMs;

    public static SingleFrameResult Empty(string? error = null) => new(
        false, Array.Empty<DinoDetectionDto>(), null,
        Array.Empty<MaskQuantificationService.QuantifiedMask>(),
        0, 0, 0, error);
}
