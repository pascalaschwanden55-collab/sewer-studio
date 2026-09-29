using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Orchestriert YOLO → DINO → SAM fuer einen einzelnen Frame.
/// Extrahiert aus MultiModelAnalysisService, ohne Video-Streaming und Temporal-Dedup.
/// Fuer den Codiermodus: "Jetzt analysieren" auf dem aktuellen Frame.
/// </summary>
public sealed class SingleFrameMultiModelService
{
    private readonly IVisionPipelineClient _client;
    private readonly double _yoloConfidence;
    private readonly double _dinoBoxThreshold;
    private readonly double _dinoTextThreshold;

    public SingleFrameMultiModelService(
        IVisionPipelineClient client,
        double? yoloConfidence = null,
        double? dinoBoxThreshold = null,
        double? dinoTextThreshold = null,
        IPipelineEnvironmentOptions? pipelineEnvironmentOptions = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        var options = pipelineEnvironmentOptions ?? PipelineEnvironmentOptions.Current;
        // Defaults respektieren dieselben Env-Vars wie der Batch-Pfad (AiSettingsFactory).
        // 0.25/0.20 seit A/B auf 57er-clean (2026-06-10) — gleiche Werte wie AiSettingsFactory.
        _yoloConfidence = yoloConfidence
            ?? options.ResolveDoubleWithCompat(PipelineEnvironmentOptions.YoloConfidenceEnvVar, 0.25);
        _dinoBoxThreshold = dinoBoxThreshold
            ?? options.ResolveDoubleWithCompat(PipelineEnvironmentOptions.DinoBoxThresholdEnvVar, 0.25);
        _dinoTextThreshold = dinoTextThreshold
            ?? options.ResolveDoubleWithCompat(PipelineEnvironmentOptions.DinoTextThresholdEnvVar, 0.20);
    }

    public SingleFrameMultiModelService(IVisionPipelineClient client, PipelineConfig config)
        : this(
            client,
            config.YoloConfidence,
            config.DinoBoxThreshold,
            config.DinoTextThreshold)
    {
    }

    /// <summary>
    /// Analysiert einen einzelnen Frame mit der Multi-Model Pipeline.
    /// </summary>
    /// <param name="pngBytes">Frame als PNG-Bytes.</param>
    /// <param name="pipeDiameterMm">Rohr-Nenndurchmesser in mm (aus Haltung).</param>
    /// <param name="calibration">Optionale Kalibrierung fuer praezisere Messungen.</param>
    /// <param name="ct">CancellationToken.</param>
    public Task<SingleFrameResult> AnalyzeFrameAsync(
        byte[] pngBytes,
        int pipeDiameterMm,
        PipeCalibration? calibration = null,
        CancellationToken ct = default,
        double? currentMeterM = null,
        double? reachLengthM = null)
        => AnalyzeCoreAsync(pngBytes, pipeDiameterMm, calibration, ct, currentMeterM, reachLengthM, null);

    /// <summary>Nur expliziter Messhost-Einstieg. Kein Kandidat wird dadurch qualifiziert.</summary>
    public async Task<SingleFrameResult> AnalyzeCandidateFrameAsync(
        byte[] pngBytes, int pipeDiameterMm, CodingDetectorCandidateFrame candidate,
        PipeCalibration? calibration = null, CancellationToken ct = default,
        double? currentMeterM = null, double? reachLengthM = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var result = await AnalyzeCoreAsync(pngBytes, pipeDiameterMm, calibration, ct, currentMeterM, reachLengthM, candidate);
        // Auch technische Fehlerantworten sind keine unbekannte oder produktive Freigabe.
        return result with { DetectorQualified = false };
    }

    private async Task<SingleFrameResult> AnalyzeCoreAsync(
        byte[] pngBytes, int pipeDiameterMm, PipeCalibration? calibration, CancellationToken ct,
        double? currentMeterM, double? reachLengthM, CodingDetectorCandidateFrame? candidate)
    {
        if (pngBytes == null || pngBytes.Length == 0)
            return SingleFrameResult.Empty("Kein Frame-Bild");
        if (candidate?.ValidateForImage(pngBytes) is { } candidateError)
            return SingleFrameResult.Empty(candidateError) with
            {
                DetectorQualified = false, Degraded = true, DegradedReason = candidateError
            };

        var b64 = Convert.ToBase64String(pngBytes);
        double yoloMs = 0, dinoMs = 0, samMs = 0, classifierMs = 0;
        var detectorQualification = await ReadDetectorQualificationAsync(ct);
        bool? effectiveDetectorQualified = candidate is null ? detectorQualification?.Qualified : false;
        var detectorApproved = effectiveDetectorQualified == true;
        var detectorQualificationReason = candidate is not null
            ? $"Entwicklungskandidat {candidate.CandidateId}; keine produktive Freigabe"
            : detectorApproved
            ? null
            : detectorQualification is null
                ? "Qualifikationsstatus fehlt oder konnte nicht gelesen werden"
                : string.IsNullOrWhiteSpace(detectorQualification.Reason)
                    ? "Detektor wurde nicht freigegeben"
                    : detectorQualification.Reason;
        var detectorReviewReason = !detectorApproved
            ? "YOLO-Detektor nicht qualifiziert"
              + (string.IsNullOrWhiteSpace(detectorQualificationReason)
                  ? string.Empty
                  : $": {detectorQualificationReason}")
              + ". DINO/SAM liefen ohne YOLO-Filter; Ergebnis manuell prüfen."
            : null;
        if (candidate is not null) yoloMs = candidate.InferenceTimeMs;

        try
        {
            VsaCodeResolver.ResolvedCode? classifierDecision = null;
            IReadOnlyList<YoloClassifyPrediction> classifierPredictions = Array.Empty<YoloClassifyPrediction>();
            // Qualitaetsgrund des Bilds (zu dunkel, zu hell …): muss bis zur Anzeige mit, sonst erscheint ein
            // unbrauchbares Bild gruen als «Kein Schaden erkannt» (Audit A03, 23.09.2026).
            string? bildQualitaet = null;
            try
            {
                var clsResp = await _client.ClassifyYoloAsync(new YoloClassifyRequest(b64, 5), ct);
                classifierMs = clsResp.InferenceTimeMs;
                if (!clsResp.Usable)
                    bildQualitaet = QualitaetsGrund(clsResp.QualityReason) ?? clsResp.QualityReason;

                if (clsResp.Usable
                    && clsResp.ClassifierLoaded
                    && !clsResp.BendVetoFailed
                    && currentMeterM.HasValue
                    && reachLengthM.HasValue)
                {
                    classifierPredictions = clsResp.Predictions;
                    classifierDecision = VsaCodeResolver.ResolveFromClassifier(
                        classifierPredictions,
                        currentMeterM.Value,
                        reachLengthM.Value,
                        isBend: clsResp.IsBend);
                    classifierDecision ??= ResolveVisibleFrameCandidateFromRawClassifier(
                        classifierPredictions,
                        currentMeterM.Value,
                        reachLengthM.Value);

                    var boundaryDecision = ResolveBoundaryFromPosition(
                        currentMeterM,
                        reachLengthM,
                        classifierDecision,
                        classifierPredictions,
                        clsResp.IsBend);

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
                            ClassifierTimeMs: classifierMs,
                            Degraded: !detectorApproved,
                            DegradedReason: detectorReviewReason,
                            DetectorQualified: effectiveDetectorQualified,
                            DetectorQualificationReason: detectorQualificationReason);
                    }
                }
            }
            catch
            {
                // Klassifizierer ist ein Zusatzsignal. Wenn er nicht verfuegbar ist,
                // bleibt der bisherige YOLO->DINO->SAM-Pfad unveraendert.
            }

            // 1. YOLO Pre-Screening. Ein ausdruecklich unqualifiziertes Modell wird
            // weder aufgerufen noch als Filter/Confidence-Beweis verwendet.
            double? yoloMax = null;
            YoloResponse? localizedYolo = null;
            if (detectorApproved)
            {
                var yoloReq = new YoloRequest(b64, _yoloConfidence);
                var yoloResp = await _client.DetectYoloAsync(yoloReq, ct);
                yoloMs = yoloResp.InferenceTimeMs;
                var qualifiedHash = detectorQualification?.Artifact?.Sha256;
                var sameArtifact = CodingLocalizedDetection.IsSha256(qualifiedHash)
                    && string.Equals(qualifiedHash,
                        yoloResp.DetectorArtifactSha256, StringComparison.OrdinalIgnoreCase);
                if (yoloResp.DetectorQualified != true || !sameArtifact)
                {
                    effectiveDetectorQualified = false;
                    detectorApproved = false;
                    detectorQualificationReason =
                        (!sameArtifact ? "YOLO-Antwort ohne passenden Artefaktnachweis" : yoloResp.DetectorQualificationReason)
                        ?? "YOLO-Antwort ohne positive Detektorqualifikation";
                    detectorReviewReason =
                        $"YOLO-Detektor nicht qualifiziert: {detectorQualificationReason}. "
                        + "DINO/SAM liefen ohne YOLO-Filter; Ergebnis manuell prüfen.";
                }
                else
                {
                    localizedYolo = yoloResp;
                    // D2-A: echte YOLO-Confidence (hoechste Box) ans QualityGate weiterreichen.
                    yoloMax = yoloResp.Detections.Count > 0
                        ? yoloResp.Detections.Max(d => d.Confidence)
                        : null;

                    if (!yoloResp.IsRelevant && !IsClassifierOnlyStructuralCode(classifierDecision?.Code))
                    {
                        var unbrauchbar = QualitaetsGrund(yoloResp.FrameClass) ?? bildQualitaet;
                        var unbrauchbarGrund = unbrauchbar is null ? null : $"Bild nicht beurteilbar: {unbrauchbar} — manuell prüfen";
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
                            ClassifierTimeMs: classifierMs,
                            Degraded: unbrauchbarGrund is not null,
                            DegradedReason: unbrauchbarGrund,
                            DetectorQualified: effectiveDetectorQualified,
                            DetectorQualificationReason: detectorQualificationReason);
                    }
                }
            }

            // 2. DINO Open-Vocabulary Detection
            var dinoReq = new DinoRequest(b64, null, _dinoBoxThreshold, _dinoTextThreshold);
            var dinoResp = await _client.DetectDinoAsync(dinoReq, ct);
            dinoMs = dinoResp.InferenceTimeMs;

            ImageSizeReader.TryRead(pngBytes, out var imageWidth, out var imageHeight);
            var localized = candidate is null
                ? CodingLocalizedDetectionPlan.Build(dinoResp.Detections, localizedYolo,
                    detectorQualification?.Artifact?.Sha256, imageWidth, imageHeight)
                : CodingLocalizedDetectionPlan.BuildCandidate(dinoResp.Detections, candidate,
                    pngBytes, imageWidth, imageHeight);
            var localizedReviewReason = localized.RejectedBoxes > 0
                ? $"{localized.RejectedBoxes} Box(en) ohne eindeutigen Klassen-, Bild- oder Modellnachweis; manuell prüfen."
                : null;

            if (localized.Detections.Count == 0)
            {
                // Leere DINO-Detektionen bei degraded=true sind KEIN "kein Schaden", sondern ein
                // Modellfehler — sonst erscheint ein DINO-Ausfall im Codiermodus als gruenes Rohr.
                var dinoDegradedReason = dinoResp.Degraded
                    ? $"DINO nicht verfügbar: {dinoResp.Error}"
                    : null;
                var emptyDinoDegradedReason = CombineReasons(
                    bildQualitaet is null ? null : $"Bild nicht beurteilbar: {bildQualitaet}",
                    detectorReviewReason, dinoDegradedReason, localizedReviewReason);
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
                    ClassifierTimeMs: classifierMs,
                    Degraded: emptyDinoDegradedReason is not null,
                    DegradedReason: emptyDinoDegradedReason,
                    DetectorQualified: effectiveDetectorQualified,
                    DetectorQualificationReason: detectorQualificationReason);
            }

            // 3. SAM segmentiert verortete Befunde; die Quelle bleibt separat erhalten.
            var samBoxes = localized.Detections.Select(d => new SamBoundingBox(
                d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence)).ToList();

            var samReq = new SamRequest(b64, samBoxes, pipeDiameterMm > 0 ? pipeDiameterMm : null);
            var samResp = await _client.SegmentSamAsync(samReq, ct);
            samMs = samResp.InferenceTimeMs;

            if (localized.Detections.Any(d => d.HasYolo)
                && (samResp.ImageWidth != imageWidth || samResp.ImageHeight != imageHeight))
                return SingleFrameResult.Empty("SAM-Bildmasse passen nicht zum analysierten Detektorbild.");

            // 4. Quantifizierung: Pixel-Masken → mm, %, Uhrposition
            var quantified = new List<MaskQuantificationService.QuantifiedMask>();
            foreach (var mask in samResp.Masks)
            {
                var q = calibration != null
                    ? MaskQuantificationService.Quantify(mask, samResp.ImageWidth, samResp.ImageHeight, pipeDiameterMm, calibration)
                    : MaskQuantificationService.Quantify(mask, samResp.ImageWidth, samResp.ImageHeight, pipeDiameterMm);
                quantified.Add(q);
            }

            var degradedReason = CombineReasons(
                detectorReviewReason,
                dinoResp.Degraded ? $"DINO: {dinoResp.Error}" : null,
                samResp.Degraded ? $"SAM: {samResp.Error}" : null,
                localizedReviewReason);
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
                ClassifierTimeMs: classifierMs,
                Degraded: degradedReason is not null,
                DegradedReason: degradedReason,
                DetectorQualified: effectiveDetectorQualified,
                DetectorQualificationReason: detectorQualificationReason,
                LocalizedDetections: localized.Detections.Select(d => d with
                {
                    RequiresReview = degradedReason is not null,
                    HasTechnicalFailure = dinoResp.Degraded || samResp.Degraded || localized.RejectedBoxes > 0
                }).ToArray());
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return SingleFrameResult.Empty($"Multi-Model Fehler: {ex.Message}");
        }
    }

    private async Task<SidecarDetectorQualification?> ReadDetectorQualificationAsync(
        CancellationToken ct)
    {
        try
        {
            var health = await _client.HealthCheckAsync(ct).ConfigureAwait(false);
            return health?.DetectorQualification;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Klartext eines Qualitaetsgrunds des Sidecars; null, wenn das Bild brauchbar ist.</summary>
    private static string? QualitaetsGrund(string? code) => code switch
    {
        "too_dark" => "zu dunkel",
        "too_bright" => "zu hell",
        "too_uniform" => "ohne Struktur",
        "too_blurry" => "unscharf",
        _ => null,
    };

    private static string? CombineReasons(params string?[] reasons)
    {
        var present = reasons.Where(reason => !string.IsNullOrWhiteSpace(reason)).ToArray();
        return present.Length == 0 ? null : string.Join("; ", present);
    }

    private static VsaCodeResolver.ResolvedCode? ResolveBoundaryFromPosition(
        double? currentMeterM,
        double? reachLengthM,
        VsaCodeResolver.ResolvedCode? classifierDecision,
        IReadOnlyList<YoloClassifyPrediction> predictions,
        bool isBend = false)
    {
        if (!currentMeterM.HasValue || !reachLengthM.HasValue)
            return classifierDecision;

        if (classifierDecision?.Code is "BCD" or "BCE")
            return classifierDecision;

        // Bogen-Veto (Geometrie aus demselben Frame): Der cls-Klassifikator hat keine
        // Bogen-Klasse und meldet Boegen als BCE 0.68; in der Endzone wuerde die Positions-
        // Regel das faelschlich zu BCE Rohrende verstaerken. Erkennt die Fluchtpunkt-
        // Geometrie einen Bogen, NICHT positionsbasiert BCE setzen.
        if (isBend)
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

    private static VsaCodeResolver.ResolvedCode? ResolveVisibleFrameCandidateFromRawClassifier(
        IReadOnlyList<YoloClassifyPrediction> predictions,
        double currentMeter,
        double totalLength)
    {
        if (predictions.Count == 0 || totalLength <= 1)
            return null;

        var top1 = predictions[0];
        var code = top1.ClassName.Trim().ToUpperInvariant();
        if (code != "BCE" || top1.Confidence < 0.65)
            return null;

        if (currentMeter >= totalLength * 0.85)
            return null;

        return new VsaCodeResolver.ResolvedCode(
            "BCE",
            top1.Confidence,
            $"YOLO BCE {top1.Confidence:P0} (sichtbarer Kandidat, Positionsprüfung im Player)");
    }

    private static bool IsClassifierOnlyStructuralCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var normalized = code.Trim().Replace(".", "").ToUpperInvariant();
        var main = normalized.Length >= 3 ? normalized[..3] : normalized;
        return main is "BCA" or "BCC";
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
    double ClassifierTimeMs = 0,
    // Degraded=true: Detektor nicht freigegeben oder DINO/SAM meldet einen Fehler.
    // Das Ergebnis darf dann nicht als gruenes "kein Schaden" gewertet werden.
    bool Degraded = false,
    string? DegradedReason = null,
    bool? DetectorQualified = null,
    string? DetectorQualificationReason = null,
    IReadOnlyList<CodingLocalizedDetection>? LocalizedDetections = null)
{
    public bool HasDetections => DinoDetections.Count > 0 || LocalizedDetections?.Count > 0;
    public bool HasMasks => SamResponse?.Masks.Count > 0;
    public double TotalTimeMs => ClassifierTimeMs + YoloTimeMs + DinoTimeMs + SamTimeMs;

    public static SingleFrameResult Empty(string? error = null) => new(
        false, Array.Empty<DinoDetectionDto>(), null,
        Array.Empty<MaskQuantificationService.QuantifiedMask>(),
        0, 0, 0, error);
}
