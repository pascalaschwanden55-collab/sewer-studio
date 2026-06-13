using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
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
        double? yoloConfidence = null,
        double? dinoBoxThreshold = null,
        double? dinoTextThreshold = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        // Defaults respektieren dieselben Env-Vars wie der Batch-Pfad (AiSettingsFactory).
        // 0.25/0.20 seit A/B auf 57er-clean (2026-06-10) — gleiche Werte wie AiSettingsFactory.
        _yoloConfidence = yoloConfidence ?? EnvDouble("SEWERSTUDIO_YOLO_CONFIDENCE") ?? 0.25;
        _dinoBoxThreshold = dinoBoxThreshold ?? EnvDouble("SEWERSTUDIO_DINO_BOX_THRESHOLD") ?? 0.25;
        _dinoTextThreshold = dinoTextThreshold ?? EnvDouble("SEWERSTUDIO_DINO_TEXT_THRESHOLD") ?? 0.20;
    }

    public SingleFrameMultiModelService(VisionPipelineClient client, PipelineConfig config)
        : this(
            client,
            config.YoloConfidence,
            config.DinoBoxThreshold,
            config.DinoTextThreshold)
    {
    }

    private static double? EnvDouble(string name)
    {
        var value = Environment.GetEnvironmentVariable(name)
                    ?? Environment.GetEnvironmentVariable("AUSWERTUNGPRO_" + name["SEWERSTUDIO_".Length..]);
        return double.TryParse(value?.Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
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
        CancellationToken ct = default,
        double? currentMeterM = null,
        double? reachLengthM = null)
    {
        if (pngBytes == null || pngBytes.Length == 0)
            return SingleFrameResult.Empty("Kein Frame-Bild");

        var b64 = Convert.ToBase64String(pngBytes);
        double yoloMs = 0, dinoMs = 0, samMs = 0, classifierMs = 0;

        try
        {
            VsaCodeResolver.ResolvedCode? classifierDecision = null;
            IReadOnlyList<YoloClassifyPrediction> classifierPredictions = Array.Empty<YoloClassifyPrediction>();
            try
            {
                var clsResp = await _client.ClassifyYoloAsync(new YoloClassifyRequest(b64, 5), ct);
                classifierMs = clsResp.InferenceTimeMs;

                if (clsResp.Usable && currentMeterM.HasValue && reachLengthM.HasValue)
                {
                    classifierPredictions = clsResp.Predictions;
                    classifierDecision = VsaCodeResolver.ResolveFromClassifier(
                        classifierPredictions,
                        currentMeterM.Value,
                        reachLengthM.Value);

                    var boundaryDecision = ResolveBoundaryFromPosition(
                        currentMeterM,
                        reachLengthM,
                        classifierDecision,
                        classifierPredictions);

                    if (boundaryDecision?.Code is "BCD" or "BCE")
                    {
                        return new SingleFrameResult(
                            IsRelevant: true,
                            DinoDetections: Array.Empty<DinoDetectionDto>(),
                            SamResponse: null,
                            QuantifiedMasks: Array.Empty<MaskQuantificationService.QuantifiedMask>(),
                            YoloTimeMs: 0,
                            DinoTimeMs: 0,
                            SamTimeMs: 0,
                            Error: null,
                            YoloMaxConfidence: null,
                            ClassifierCode: boundaryDecision.Code,
                            ClassifierConfidence: boundaryDecision.Confidence,
                            ClassifierSource: boundaryDecision.Source,
                            ClassifierTimeMs: classifierMs);
                    }
                }
            }
            catch
            {
                // Klassifizierer ist ein Zusatzsignal. Wenn er nicht verfuegbar ist,
                // bleibt der bisherige YOLO->DINO->SAM-Pfad unveraendert.
            }

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
                    Error: null, YoloMaxConfidence: yoloMax,
                    ClassifierCode: classifierDecision?.Code,
                    ClassifierConfidence: classifierDecision?.Confidence,
                    ClassifierSource: classifierDecision?.Source,
                    ClassifierTimeMs: classifierMs);
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
                    Error: null, YoloMaxConfidence: yoloMax,
                    ClassifierCode: classifierDecision?.Code,
                    ClassifierConfidence: classifierDecision?.Confidence,
                    ClassifierSource: classifierDecision?.Source,
                    ClassifierTimeMs: classifierMs);
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
                Error: null, YoloMaxConfidence: yoloMax,
                ClassifierCode: classifierDecision?.Code,
                ClassifierConfidence: classifierDecision?.Confidence,
                ClassifierSource: classifierDecision?.Source,
                ClassifierTimeMs: classifierMs);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return SingleFrameResult.Empty($"Multi-Model Fehler: {ex.Message}");
        }
    }

    private static VsaCodeResolver.ResolvedCode? ResolveBoundaryFromPosition(
        double? currentMeterM,
        double? reachLengthM,
        VsaCodeResolver.ResolvedCode? classifierDecision,
        IReadOnlyList<YoloClassifyPrediction> predictions)
    {
        if (!currentMeterM.HasValue || !reachLengthM.HasValue)
            return classifierDecision;

        if (classifierDecision?.Code is "BCD" or "BCE")
            return classifierDecision;

        if (classifierDecision is { Code: not ("LEER" or "OTHER") })
            return classifierDecision;

        if (predictions.Count == 0)
            return classifierDecision;

        var meter = currentMeterM.Value;
        var length = reachLengthM.Value;
        if (length <= 1)
            return classifierDecision;

        var endToleranceM = Math.Max(0.5, length * 0.02);
        if (meter < length - endToleranceM)
            return classifierDecision;

        var bceConfidence = predictions
            .FirstOrDefault(p => string.Equals(p.ClassName, "BCE", StringComparison.OrdinalIgnoreCase))
            ?.Confidence ?? 0;

        return new VsaCodeResolver.ResolvedCode(
            "BCE",
            Math.Max(bceConfidence, 0.80),
            $"Endzone {meter:F2}/{length:F1}m + YOLO BCE {bceConfidence:P0}");
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
    double? YoloMaxConfidence = null,
    string? ClassifierCode = null,
    double? ClassifierConfidence = null,
    string? ClassifierSource = null,
    double ClassifierTimeMs = 0)
{
    public bool HasDetections => DinoDetections.Count > 0;
    public bool HasMasks => SamResponse?.Masks.Count > 0;
    public double TotalTimeMs => ClassifierTimeMs + YoloTimeMs + DinoTimeMs + SamTimeMs;

    public static SingleFrameResult Empty(string? error = null) => new(
        false, Array.Empty<DinoDetectionDto>(), null,
        Array.Empty<MaskQuantificationService.QuantifiedMask>(),
        0, 0, 0, error);
}
