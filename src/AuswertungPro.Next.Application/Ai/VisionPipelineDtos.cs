using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Ai;

// ── Health ─────────────────────────────────────────────────────────────────

public sealed record SidecarHealthResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("gpu")] GpuStatus? Gpu,
    [property: JsonPropertyName("models_present")] SidecarModelsPresent? ModelsPresent = null,
    [property: JsonPropertyName("process_id")] int? ProcessId = null,
    [property: JsonPropertyName("classifier")] SidecarClassifierStatus? Classifier = null,
    // Qualifikationsstand des aktiven Detektors (additiv; null bei aelterem Sidecar
    // ohne das Feld). Verbraucher duerfen nur ein ausdrueckliches true freigeben.
    [property: JsonPropertyName("detector_qualification")] SidecarDetectorQualification? DetectorQualification = null
)
{
    [JsonIgnore]
    public IReadOnlyList<string> MissingRequiredModels
    {
        get
        {
            var missing = new List<string>();
            if (ModelsPresent is { Dino: false })
                missing.Add("DINO");
            if (ModelsPresent is { Sam: false })
                missing.Add("SAM");
            return missing;
        }
    }

    [JsonIgnore]
    public bool HasRequiredModels => MissingRequiredModels.Count == 0;

    [JsonIgnore]
    public string MissingRequiredModelsText => string.Join(", ", MissingRequiredModels);

    /// <summary>
    /// True nur, wenn der Sidecar explizit "classifier.loaded=false" meldet.
    /// Fehlt das Feld (aelterer Sidecar ohne classifier im Health-Response), gilt der
    /// Klassifikator als unbekannt - nicht als fehlend (fail-open fuer Alt-Sidecars).
    /// </summary>
    [JsonIgnore]
    public bool ClassifierMissing => Classifier is { Loaded: false };
}

/// <summary>
/// Status des VSA-Klassifikators aus dem /health-Feld "classifier".
/// Geladen: name/source/sha256_12/imgsz/preprocessing; nicht geladen: nur loaded=false
/// (+ active_json_present/override_configured, hier nicht abgebildet).
/// </summary>
public sealed record SidecarClassifierStatus(
    [property: JsonPropertyName("loaded")] bool Loaded,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("source")] string? Source = null
);

public sealed record SidecarModelsPresent(
    [property: JsonPropertyName("dino")] bool Dino,
    [property: JsonPropertyName("sam")] bool Sam
);

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

// ── YOLO ───────────────────────────────────────────────────────────────────

public sealed record YoloRequest(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("confidence_threshold")] double ConfidenceThreshold
);

/// <summary>
/// Getrennter BCC-Testrequest. Es werden nur eine Kandidaten-ID und der erwartete
/// Gewicht-Hash uebertragen, niemals ein Modellpfad.
/// </summary>
public sealed record BccTestYoloRequest(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("confidence_threshold")] double ConfidenceThreshold,
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("candidate_sha256")] string CandidateSha256,
    [property: JsonPropertyName("meter_format")] string? MeterFormat = null
);

public sealed record YoloDetectionDto(
    [property: JsonPropertyName("x1")] double X1,
    [property: JsonPropertyName("y1")] double Y1,
    [property: JsonPropertyName("x2")] double X2,
    [property: JsonPropertyName("y2")] double Y2,
    [property: JsonPropertyName("class_name")] string ClassName,
    [property: JsonPropertyName("confidence")] double Confidence
);

public sealed record YoloResponse(
    [property: JsonPropertyName("is_relevant")] bool IsRelevant,
    [property: JsonPropertyName("detections")] IReadOnlyList<YoloDetectionDto> Detections,
    [property: JsonPropertyName("frame_class")] string FrameClass,
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs,
    [property: JsonPropertyName("model_name")] string? ModelName = null,
    [property: JsonPropertyName("model_backend")] string? ModelBackend = null,
    [property: JsonPropertyName("device")] string? Device = null,
    [property: JsonPropertyName("queue_wait_ms")] double QueueWaitMs = 0,
    [property: JsonPropertyName("vram_allocated_gb")] double? VramAllocatedGb = null,
    [property: JsonPropertyName("vram_total_gb")] double? VramTotalGb = null,
    [property: JsonPropertyName("gpu_utilization_percent")] double? GpuUtilizationPercent = null,
    [property: JsonPropertyName("detector_qualified")] bool? DetectorQualified = null,
    [property: JsonPropertyName("detector_qualification_status")] string DetectorQualificationStatus = "not_checked",
    [property: JsonPropertyName("detector_qualification_reason")] string? DetectorQualificationReason = null,
    [property: JsonPropertyName("detector_artifact_sha256")] string? DetectorArtifactSha256 = null
);

/// <summary>
/// Antwort des getrennten, nicht produktiven BCC-Trainingskandidaten.
/// <see cref="Available"/> ist false, wenn kein sicher pruefbarer Kandidat bereitsteht.
/// </summary>
public sealed record BccTestYoloResponse(
    [property: JsonPropertyName("available")] bool Available,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("is_relevant")] bool IsRelevant,
    [property: JsonPropertyName("detections")] IReadOnlyList<YoloDetectionDto> Detections,
    [property: JsonPropertyName("frame_class")] string FrameClass,
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs,
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("candidate_sha256")] string CandidateSha256,
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("device")] string Device,
    [property: JsonPropertyName("frame_usable")] bool FrameUsable = true,
    [property: JsonPropertyName("quality_reason")] string? QualityReason = null,
    [property: JsonPropertyName("meter_value")] double? MeterValue = null
);

/// <summary>Pfadfreie Metadaten eines manifest- und hashgeprueften BCC-Testkandidaten.</summary>
public sealed record BccTestCandidateInfo(
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("candidate_sha256")] string CandidateSha256,
    [property: JsonPropertyName("map50")] double Map50,
    [property: JsonPropertyName("epochs_completed")] int EpochsCompleted,
    [property: JsonPropertyName("created_utc")] string CreatedUtc
);

public sealed record BccTestCandidatesResponse(
    [property: JsonPropertyName("available")] bool Available,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("candidates")] IReadOnlyList<BccTestCandidateInfo> Candidates
);

// ── YOLO Classify ─────────────────────────────────────────────────────────

public sealed record YoloClassifyRequest(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("top_k")] int TopK = 5
);

public sealed record YoloClassifyPrediction(
    [property: JsonPropertyName("class_name")] string ClassName,
    [property: JsonPropertyName("confidence")] double Confidence
);

public sealed record YoloClassifyResponse(
    [property: JsonPropertyName("predictions")] IReadOnlyList<YoloClassifyPrediction> Predictions,
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs,
    // Frame-Quality-Gate des Sidecars: usable=false -> Frame ist schwarz/ueberbelichtet/
    // strukturlos/unscharf und soll gar nicht erst durch DINO/SAM/Qwen laufen.
    [property: JsonPropertyName("usable")] bool Usable = true,
    [property: JsonPropertyName("quality_reason")] string QualityReason = "ok",
    // Modell-Governance (active.json-Weg): welches cls-Modell hat geantwortet
    [property: JsonPropertyName("model_name")] string ModelName = "",
    [property: JsonPropertyName("model_source")] string ModelSource = "",
    // Fail-closed wie der Sidecar (schemas/detection.py: classifier_loaded=False als Default):
    // Fehlt das Feld in einer Antwort (aelterer/fremder Sidecar), gilt der Klassifikator als
    // NICHT geladen, statt faelschlich Klassifikator-Codes anzuwenden.
    [property: JsonPropertyName("classifier_loaded")] bool ClassifierLoaded = false,
    [property: JsonPropertyName("model_sha256")] string ModelSha256 = "",
    [property: JsonPropertyName("imgsz")] int Imgsz = 0,
    [property: JsonPropertyName("preprocessing")] string Preprocessing = "",
    [property: JsonPropertyName("device")] string Device = "",
    // Geometrisches Bogen-Veto (BCC) aus demselben Frame: is_bend=true -> Bogen erkannt,
    // Frame NICHT als BCE Rohrende codieren (der cls hat keine Bogen-Klasse).
    [property: JsonPropertyName("bend_shift")] double BendShift = 0.0,
    [property: JsonPropertyName("is_bend")] bool IsBend = false,
    [property: JsonPropertyName("bend_veto_failed")] bool BendVetoFailed = false,
    [property: JsonPropertyName("vanish_x")] double VanishX = 0.5,
    [property: JsonPropertyName("vanish_y")] double VanishY = 0.5
);

// ── Grounding DINO ─────────────────────────────────────────────────────────

public sealed record DinoRequest(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("text_prompt")] string? TextPrompt,
    [property: JsonPropertyName("box_threshold")] double BoxThreshold,
    [property: JsonPropertyName("text_threshold")] double TextThreshold
);

public sealed record DinoDetectionDto(
    [property: JsonPropertyName("x1")] double X1,
    [property: JsonPropertyName("y1")] double Y1,
    [property: JsonPropertyName("x2")] double X2,
    [property: JsonPropertyName("y2")] double Y2,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("phrase")] string Phrase
);

public sealed record DinoResponse(
    [property: JsonPropertyName("detections")] IReadOnlyList<DinoDetectionDto> Detections,
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs,
    // Ehrlichkeits-Felder des Sidecars: degraded=true bedeutet Modell-/Inferenzfehler ->
    // leere detections sind dann KEIN sauberer Negativbefund, sondern Review-Signal.
    [property: JsonPropertyName("degraded")] bool Degraded = false,
    [property: JsonPropertyName("error")] string? Error = null,
    [property: JsonPropertyName("error_code")] string? ErrorCode = null
);

// ── SAM ────────────────────────────────────────────────────────────────────

public sealed record SamBoundingBox(
    [property: JsonPropertyName("x1")] double X1,
    [property: JsonPropertyName("y1")] double Y1,
    [property: JsonPropertyName("x2")] double X2,
    [property: JsonPropertyName("y2")] double Y2,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("confidence")] double Confidence
);

public sealed record SamRequest(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("bounding_boxes")] IReadOnlyList<SamBoundingBox> BoundingBoxes,
    [property: JsonPropertyName("pipe_diameter_mm")] int? PipeDiameterMm
);

public sealed record SamMaskResult(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("bbox")] IReadOnlyList<double> Bbox,
    [property: JsonPropertyName("mask_rle")] string MaskRle,
    [property: JsonPropertyName("mask_area_pixels")] int MaskAreaPixels,
    [property: JsonPropertyName("image_area_pixels")] int ImageAreaPixels,
    [property: JsonPropertyName("height_pixels")] int HeightPixels,
    [property: JsonPropertyName("width_pixels")] int WidthPixels,
    [property: JsonPropertyName("centroid_x")] double CentroidX,
    [property: JsonPropertyName("centroid_y")] double CentroidY
);

public sealed record SamResponse(
    [property: JsonPropertyName("masks")] IReadOnlyList<SamMaskResult> Masks,
    [property: JsonPropertyName("image_width")] int ImageWidth,
    [property: JsonPropertyName("image_height")] int ImageHeight,
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs,
    // Ehrlichkeits-Felder: degraded=true, sobald Boxen verloren gingen (skipped_boxes)
    // oder ein Fehler auftrat -> Teil-Segmentierung, Review-Signal.
    [property: JsonPropertyName("degraded")] bool Degraded = false,
    [property: JsonPropertyName("requested_boxes")] int RequestedBoxes = 0,
    [property: JsonPropertyName("skipped_boxes")] int SkippedBoxes = 0,
    // Teilmenge von skipped_boxes: Masken unterhalb des SAM-Score-Gates (sam_min_score)
    [property: JsonPropertyName("low_score_boxes")] int LowScoreBoxes = 0,
    [property: JsonPropertyName("error")] string? Error = null,
    // Geometrisches Bogen-Signal aus demselben Frame: is_bend=true -> Bogen erkannt,
    // vanish_x/y = Fluchtpunkt (wo das Rohr abknickt), normiert 0..1. Wird im Codiermodus
    // als Bogen-Marker gezeigt (eine saubere SAM-Maske der Bogen-Kontur ist nicht moeglich).
    [property: JsonPropertyName("bend_shift")] double BendShift = 0.0,
    [property: JsonPropertyName("is_bend")] bool IsBend = false,
    [property: JsonPropertyName("vanish_x")] double VanishX = 0.5,
    [property: JsonPropertyName("vanish_y")] double VanishY = 0.5
);

/// <summary>Qualifikationsstand des aktiven Detektors aus der Sidecar-Statusdatei.</summary>
public sealed record SidecarDetectorQualification(
    [property: JsonPropertyName("qualified")] bool Qualified,
    [property: JsonPropertyName("reason")] string? Reason
);

// ── Training Export ─────────────────────────────────────────────────────────

// Plan-gesteuerter v2-Vertrag. Split, Klassen-IDs und Dateinamen stammen
// ausschliesslich aus dem zuvor erzeugten C#-Exportplan.
public sealed record TrainingExportPlanLabelDto(
    [property: JsonPropertyName("class_id")] int ClassId,
    [property: JsonPropertyName("x_center")] double XCenter,
    [property: JsonPropertyName("y_center")] double YCenter,
    [property: JsonPropertyName("width")] double Width,
    [property: JsonPropertyName("height")] double Height
);

public sealed record TrainingExportPlanSampleDto(
    [property: JsonPropertyName("image_sha256")] string ImageSha256,
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("split")] string Split,
    [property: JsonPropertyName("target_file_name")] string TargetFileName,
    [property: JsonPropertyName("labels")] IReadOnlyList<TrainingExportPlanLabelDto> Labels
);

public sealed record TrainingExportPlanRequestDto(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("plan_id")] string PlanId,
    [property: JsonPropertyName("plan_sha256")] string PlanSha256,
    [property: JsonPropertyName("class_map_version")] int ClassMapVersion,
    [property: JsonPropertyName("vsa_manifest_hash")] string VsaManifestHash,
    [property: JsonPropertyName("registry_hash")] string RegistryHash,
    [property: JsonPropertyName("classes")] IReadOnlyList<string> Classes,
    [property: JsonPropertyName("manifest_json_base64")] string ManifestJsonBase64,
    [property: JsonPropertyName("manifest_sha256")] string ManifestSha256,
    [property: JsonPropertyName("samples")] IReadOnlyList<TrainingExportPlanSampleDto> Samples
);

public sealed record TrainingExportPlanResponseDto(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("plan_id")] string PlanId,
    [property: JsonPropertyName("plan_sha256")] string PlanSha256,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("total_samples")] int TotalSamples,
    [property: JsonPropertyName("train_count")] int TrainCount,
    [property: JsonPropertyName("val_count")] int ValidationCount,
    [property: JsonPropertyName("class_count")] int ClassCount,
    [property: JsonPropertyName("dataset_path")] string DatasetPath,
    [property: JsonPropertyName("data_yaml_path")] string DataYamlPath,
    [property: JsonPropertyName("manifest_path")] string ManifestPath,
    [property: JsonPropertyName("written_image_sha256")] IReadOnlyList<string> WrittenImageSha256
);

// ── Multi-Model Frame Result (internal) ────────────────────────────────────

public sealed record MultiModelFrameResult(
    double TimestampSec,
    double? Meter,
    bool IsRelevant,
    IReadOnlyList<DinoDetectionDto> DinoDetections,
    IReadOnlyList<SamMaskResult> SamMasks,
    int ImageWidth,
    int ImageHeight,
    double YoloTimeMs,
    double DinoTimeMs,
    double SamTimeMs
);

/// <summary>
/// Detailliertes Ergebnis eines Health-Checks. Unterscheidet offline, nicht autorisiert und bereit.
/// </summary>
public sealed record PipelineHealthCheckResult(
    bool IsReachable,
    bool IsAuthorized,
    int? StatusCode,
    SidecarHealthResponse? Health,
    string? Error);
