using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

// ── Health ─────────────────────────────────────────────────────────────────

public sealed record SidecarHealthResponse(
    [property: JsonPropertyName("status")]  string      Status,
    [property: JsonPropertyName("version")] string      Version,
    [property: JsonPropertyName("gpu")]     GpuStatus?  Gpu,
    [property: JsonPropertyName("yolo")]    YoloRuntimeStatus? Yolo,
    [property: JsonPropertyName("nvdec")]   NvdecStatus? Nvdec,
    [property: JsonPropertyName("vsr")]     VsrStatus?   Vsr
);

public sealed record GpuStatus(
    [property: JsonPropertyName("current_model")] string CurrentModel,
    [property: JsonPropertyName("vram_allocated_gb")] double VramAllocatedGb,
    [property: JsonPropertyName("vram_total_gb")] double VramTotalGb,
    [property: JsonPropertyName("vram_free_mb")] int VramFreeMb = 0,
    [property: JsonPropertyName("all_resident")] bool AllResident = false,
    [property: JsonPropertyName("prewarm_done")] bool PrewarmDone = false
);

public sealed record YoloRuntimeStatus(
    [property: JsonPropertyName("configured_model_name")] string? ConfiguredModelName,
    [property: JsonPropertyName("resolved_model_path")] string? ResolvedModelPath,
    [property: JsonPropertyName("active_model_path")] string? ActiveModelPath,
    [property: JsonPropertyName("tensorrt_active")] bool TensorrtActive = false,
    [property: JsonPropertyName("maintenance_reason")] string? MaintenanceReason = null,
    [property: JsonPropertyName("active_inference")] int ActiveInference = 0
);

// ── YOLO ───────────────────────────────────────────────────────────────────

public sealed record YoloRequest(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("confidence_threshold")] double ConfidenceThreshold
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
    // True wenn YOLO ohne Custom-Weights laeuft (is_relevant ist dann fallback-bedingt true).
    [property: JsonPropertyName("is_fallback_mode")] bool IsFallbackMode = false
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
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs
);

// ── ViewType Classify (Aufnahmetechnik) ───────────────────────────────────

public sealed record ViewTypeRequest(
    [property: JsonPropertyName("image_base64")] string ImageBase64);

public sealed record ViewTypePrediction(
    [property: JsonPropertyName("view_type")] string ViewType,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("all_scores")] Dictionary<string, double> AllScores);

public sealed record ViewTypeResponse(
    [property: JsonPropertyName("prediction")] ViewTypePrediction Prediction,
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs);

// ── V4.2 Phase 3: DINOv2 Foundation-Encoder + Linear-Heads ───────────────

public sealed record DinoV2Request(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("target_codes")] IReadOnlyList<string>? TargetCodes = null
);

public sealed record DinoV2Prediction(
    [property: JsonPropertyName("vsa_code")] string VsaCode,
    [property: JsonPropertyName("severity_class")] string SeverityClass,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("scores")] Dictionary<string, double> Scores
);

public sealed record DinoV2Response(
    [property: JsonPropertyName("predictions")] IReadOnlyList<DinoV2Prediction> Predictions,
    [property: JsonPropertyName("heads_loaded")] IReadOnlyList<string> HeadsLoaded,
    [property: JsonPropertyName("encoder_inference_time_ms")] double EncoderInferenceTimeMs,
    [property: JsonPropertyName("heads_inference_time_ms")] double HeadsInferenceTimeMs,
    [property: JsonPropertyName("total_time_ms")] double TotalTimeMs,
    // V4.2 Nachbesserung B: Versionierung fuer Ursachenanalyse.
    [property: JsonPropertyName("encoder_version")] string EncoderVersion = "",
    [property: JsonPropertyName("heads_manifest_hash")] string HeadsManifestHash = ""
);

// ── Florence-2 (Open-Vocabulary Detection, Slot: DINO) ────────────────────

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
    [property: JsonPropertyName("phrase")] string Phrase,
    // Provenance: Wenn diese Detektion aus YOLO-Fallback stammt (DINO leer geliefert),
    // ist IsFallbackFromYolo=true. QualityGate behandelt sie dann nicht als unabhaengige Evidenz.
    [property: JsonPropertyName("is_fallback_from_yolo")] bool IsFallbackFromYolo = false
);

public sealed record DinoResponse(
    [property: JsonPropertyName("detections")] IReadOnlyList<DinoDetectionDto> Detections,
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs
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

/// <summary>Punkt-Prompt fuer SAM: x/y in Pixel, Label=1 positiv, Label=0 negativ.</summary>
public sealed record SamPointPrompt(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("label")] int Label  // 1=positiv, 0=negativ
);

/// <summary>Ring-Scan Parameter: SAM tastet den Annulus (Rohrwand) systematisch ab.</summary>
public sealed record RingScanParams(
    [property: JsonPropertyName("center_x")] double CenterX,
    [property: JsonPropertyName("center_y")] double CenterY,
    [property: JsonPropertyName("inner_radius")] double InnerRadius,
    [property: JsonPropertyName("outer_radius")] double OuterRadius,
    [property: JsonPropertyName("num_angles")] int NumAngles = 24,
    [property: JsonPropertyName("num_radii")] int NumRadii = 3,
    [property: JsonPropertyName("min_score")] double MinScore = 0.25,
    [property: JsonPropertyName("min_area_pixels")] int MinAreaPixels = 100,
    [property: JsonPropertyName("iou_threshold")] double IouThreshold = 0.4
);

public sealed record SamRequest(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("bounding_boxes")] IReadOnlyList<SamBoundingBox> BoundingBoxes,
    [property: JsonPropertyName("point_prompts")] IReadOnlyList<SamPointPrompt>? PointPrompts = null,
    [property: JsonPropertyName("pipe_diameter_mm")] int? PipeDiameterMm = null,
    [property: JsonPropertyName("ring_scan")] RingScanParams? RingScan = null
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
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs
);

// ── Training Export ─────────────────────────────────────────────────────────

public sealed record TrainingExportSampleLabel(
    [property: JsonPropertyName("class_name")] string ClassName,
    [property: JsonPropertyName("x_center")] double XCenter,
    [property: JsonPropertyName("y_center")] double YCenter,
    [property: JsonPropertyName("width")] double Width,
    [property: JsonPropertyName("height")] double Height
);

public sealed record TrainingExportSample(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("labels")] IReadOnlyList<TrainingExportSampleLabel> Labels
);

public sealed record TrainingExportRequestDto(
    [property: JsonPropertyName("samples")] IReadOnlyList<TrainingExportSample> Samples,
    [property: JsonPropertyName("output_dir")] string OutputDir,
    [property: JsonPropertyName("train_split")] double TrainSplit
);

public sealed record TrainingExportResponseDto(
    [property: JsonPropertyName("total_samples")] int TotalSamples,
    [property: JsonPropertyName("train_count")] int TrainCount,
    [property: JsonPropertyName("val_count")] int ValCount,
    [property: JsonPropertyName("classes_used")] IReadOnlyList<string> ClassesUsed,
    [property: JsonPropertyName("data_yaml_path")] string DataYamlPath
);

public sealed record YoloTrainRequestDto(
    [property: JsonPropertyName("dataset_path")] string DatasetPath,
    [property: JsonPropertyName("epochs")] int Epochs = 50,
    [property: JsonPropertyName("imgsz")] int ImageSize = 640,
    [property: JsonPropertyName("batch")] int Batch = -1,
    [property: JsonPropertyName("base_model")] string BaseModel = "yolo11m.pt",
    [property: JsonPropertyName("project")] string Project = "runs/train",
    [property: JsonPropertyName("amp")] bool Amp = true,
    [property: JsonPropertyName("max_fallback_ratio")] double MaxFallbackRatio = 0.35
);

public sealed record YoloTrainJobStartDto(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message
);

public sealed record YoloTrainMetricsDto(
    [property: JsonPropertyName("precision")] double Precision,
    [property: JsonPropertyName("recall")] double Recall,
    [property: JsonPropertyName("f1")] double F1,
    [property: JsonPropertyName("map50")] double Map50,
    [property: JsonPropertyName("map50_95")] double Map50_95
);

public sealed record YoloDatasetQualityDto(
    [property: JsonPropertyName("total_samples")] int TotalSamples,
    [property: JsonPropertyName("total_labels")] int TotalLabels,
    [property: JsonPropertyName("fallback_labels")] int FallbackLabels,
    [property: JsonPropertyName("fallback_ratio")] double FallbackRatio,
    [property: JsonPropertyName("distinct_classes")] int DistinctClasses
);

public sealed record YoloTrainJobStatusDto(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("model_path")] string? ModelPath,
    [property: JsonPropertyName("metrics")] YoloTrainMetricsDto? Metrics,
    [property: JsonPropertyName("dataset_quality")] YoloDatasetQualityDto? DatasetQuality,
    [property: JsonPropertyName("epochs_completed")] int EpochsCompleted,
    [property: JsonPropertyName("started_utc")] DateTimeOffset? StartedUtc,
    [property: JsonPropertyName("finished_utc")] DateTimeOffset? FinishedUtc
);

public sealed record ModelReloadRequestDto(
    [property: JsonPropertyName("model_path")] string ModelPath,
    [property: JsonPropertyName("wait_timeout_sec")] double WaitTimeoutSec = 30
);

public sealed record ModelReloadResponseDto(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("resolved_model_path")] string ResolvedModelPath,
    [property: JsonPropertyName("tensorrt_active")] bool TensorrtActive
);

// ── LoRA Training ───────────────────────────────────────────────────────────

public sealed record LoraTrainSampleDto(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("expected_response")] string ExpectedResponse
);

public sealed record LoraTrainRequestDto(
    [property: JsonPropertyName("samples")] List<LoraTrainSampleDto> Samples,
    [property: JsonPropertyName("base_model")] string BaseModel = "Qwen/Qwen2.5-VL-7B-Instruct",
    [property: JsonPropertyName("lora_rank")] int LoraRank = 16,
    [property: JsonPropertyName("lora_alpha")] int LoraAlpha = 32,
    [property: JsonPropertyName("epochs")] int Epochs = 3,
    [property: JsonPropertyName("learning_rate")] double LearningRate = 2e-4,
    [property: JsonPropertyName("batch_size")] int BatchSize = 1,
    [property: JsonPropertyName("max_seq_length")] int MaxSeqLength = 4096,
    [property: JsonPropertyName("output_dir")] string OutputDir = "runs/lora"
);

public sealed record LoraTrainJobStartDto(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message
);

public sealed record LoraTrainMetricsDto(
    [property: JsonPropertyName("train_loss")] double TrainLoss,
    [property: JsonPropertyName("eval_loss")] double EvalLoss,
    [property: JsonPropertyName("epochs_completed")] int EpochsCompleted,
    [property: JsonPropertyName("samples_trained")] int SamplesTrained
);

public sealed record LoraTrainJobStatusDto(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("adapter_path")] string? AdapterPath,
    [property: JsonPropertyName("metrics")] LoraTrainMetricsDto? Metrics,
    [property: JsonPropertyName("started_utc")] DateTimeOffset? StartedUtc,
    [property: JsonPropertyName("finished_utc")] DateTimeOffset? FinishedUtc
);

public sealed record LoraDeployRequestDto(
    [property: JsonPropertyName("adapter_path")] string AdapterPath,
    [property: JsonPropertyName("base_model")] string BaseModel = "qwen3-vl:8b",
    [property: JsonPropertyName("model_name")] string ModelName = "qwen3-vl:8b-lora",
    [property: JsonPropertyName("ollama_base_url")] string OllamaBaseUrl = "http://localhost:11434"
);

public sealed record LoraDeployResponseDto(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("message")] string Message
);

// ── Pipe-Axis (Knick-Detektion) ─────────────────────────────────────────────

/// <summary>Request fuer Fluchtpunkt-Analyse eines Frames.</summary>
public sealed record PipeAxisRequest(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("pipe_diameter_mm")] int? PipeDiameterMm = null
);

/// <summary>Erkannter Fluchtpunkt + Rohrmitte eines Frames.</summary>
public sealed record PipeAxisResult(
    [property: JsonPropertyName("vanishing_x")] double VanishingX,
    [property: JsonPropertyName("vanishing_y")] double VanishingY,
    [property: JsonPropertyName("pipe_center_x")] double PipeCenterX,
    [property: JsonPropertyName("pipe_center_y")] double PipeCenterY,
    [property: JsonPropertyName("pipe_radius_x")] double PipeRadiusX,
    [property: JsonPropertyName("pipe_radius_y")] double PipeRadiusY,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("has_joint")] bool HasJoint,
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs
);

// ── NVDEC / Video-Processing ────────────────────────────────────────────────

public sealed record VideoProcessRequest(
    [property: JsonPropertyName("video_path")]            string VideoPath,
    [property: JsonPropertyName("step_seconds")]          double StepSeconds,
    [property: JsonPropertyName("confidence")]            double Confidence,
    [property: JsonPropertyName("enhance")]               bool Enhance = false,
    [property: JsonPropertyName("enhance_target_height")] int EnhanceTargetHeight = 1080,
    [property: JsonPropertyName("max_width")]             int MaxWidth = 1280
);

/// <summary>Eine NDJSON-Zeile aus dem /process/video Stream.</summary>
public sealed record VideoFrameStreamResult(
    [property: JsonPropertyName("type")]          string Type,
    // Header-Felder
    [property: JsonPropertyName("duration_sec")]          double? DurationSec,
    [property: JsonPropertyName("total_frames_estimate")] int?    TotalFramesEstimate,
    [property: JsonPropertyName("nvdec_available")]       bool?   NvdecAvailable,
    // Frame-Felder
    [property: JsonPropertyName("timestamp_sec")]  double?  TimestampSec,
    [property: JsonPropertyName("frame_index")]    int?     FrameIndex,
    [property: JsonPropertyName("is_relevant")]    bool?    IsRelevant,
    [property: JsonPropertyName("frame_class")]    string?  FrameClass,
    [property: JsonPropertyName("detections")]     IReadOnlyList<YoloDetectionDto>? Detections,
    [property: JsonPropertyName("image_base64")]   string?  ImageBase64,
    [property: JsonPropertyName("image_width")]    int?     ImageWidth,
    [property: JsonPropertyName("image_height")]   int?     ImageHeight,
    [property: JsonPropertyName("yolo_ms")]        double?  YoloMs,
    [property: JsonPropertyName("backend")]        string?  Backend,
    // Footer-Felder
    [property: JsonPropertyName("frames_processed")] int?  FramesProcessed,
    // Fehler
    [property: JsonPropertyName("error")]          string?  Error
);

// ── Video Super Resolution ───────────────────────────────────────────────────

public sealed record EnhanceRequest(
    [property: JsonPropertyName("image_base64")]   string ImageBase64,
    [property: JsonPropertyName("target_height")]  int    TargetHeight = 1080,
    [property: JsonPropertyName("denoise")]        bool   Denoise = true
);

public sealed record EnhanceResponse(
    [property: JsonPropertyName("enhanced_base64")]  string EnhancedBase64,
    [property: JsonPropertyName("processing_time_ms")] double ProcessingTimeMs,
    [property: JsonPropertyName("input_width")]      int    InputWidth,
    [property: JsonPropertyName("input_height")]     int    InputHeight,
    [property: JsonPropertyName("output_width")]     int    OutputWidth,
    [property: JsonPropertyName("output_height")]    int    OutputHeight,
    [property: JsonPropertyName("scale_factor")]     double ScaleFactor,
    [property: JsonPropertyName("backend")]          string Backend
);

// ── Sidecar Health (erweitert) ───────────────────────────────────────────────

public sealed record NvdecStatus(
    [property: JsonPropertyName("nvdec_available")] bool   NvdecAvailable,
    [property: JsonPropertyName("nvdec_backend")]   string NvdecBackend,
    [property: JsonPropertyName("nvdec_error")]     string? NvdecError
);

public sealed record VsrStatus(
    [property: JsonPropertyName("vsr_enabled")]         bool   VsrEnabled,
    [property: JsonPropertyName("vsr_backend")]         string VsrBackend,
    [property: JsonPropertyName("vsr_min_resolution")]  int    VsrMinResolution
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

// ── Parse (Nemotron-Parse PDF-Tabellen) ──────────────────────────────────

public sealed record ParsePdfTableRequest(
    [property: JsonPropertyName("pdf_base64")] string PdfBase64,
    [property: JsonPropertyName("page_numbers")] IReadOnlyList<int>? PageNumbers = null,
    [property: JsonPropertyName("table_format")] string TableFormat = "auto"
);

public sealed record ParsedRowDto(
    [property: JsonPropertyName("meter_start")] double? MeterStart,
    [property: JsonPropertyName("meter_end")] double? MeterEnd,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("char1")] string Char1,
    [property: JsonPropertyName("char2")] string Char2,
    [property: JsonPropertyName("clock_from")] string ClockFrom,
    [property: JsonPropertyName("clock_to")] string ClockTo,
    [property: JsonPropertyName("remark")] string Remark,
    [property: JsonPropertyName("severity")] int? Severity,
    [property: JsonPropertyName("raw_text")] string RawText
);

public sealed record ParsedTableDto(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("rows")] IReadOnlyList<ParsedRowDto> Rows,
    [property: JsonPropertyName("format_detected")] string FormatDetected,
    [property: JsonPropertyName("confidence")] double Confidence
);

public sealed record ParsePdfTableResponse(
    [property: JsonPropertyName("tables")] IReadOnlyList<ParsedTableDto> Tables,
    [property: JsonPropertyName("total_rows")] int TotalRows,
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs,
    [property: JsonPropertyName("model_used")] string ModelUsed
);

// ── Batch-DTOs (YOLO / DINO / SAM) ──────────────────────────────────────────

// ── YOLO Batch ──

public sealed record YoloBatchItemDto(
    [property: JsonPropertyName("image_base64")] string ImageBase64,
    [property: JsonPropertyName("frame_id")]     string FrameId = ""
);

public sealed record YoloBatchRequestDto(
    [property: JsonPropertyName("items")]                List<YoloBatchItemDto> Items,
    [property: JsonPropertyName("confidence_threshold")] double ConfidenceThreshold = 0.25
);

public sealed record YoloBatchResultItemDto(
    [property: JsonPropertyName("frame_id")] string FrameId,
    [property: JsonPropertyName("result")]   YoloResponse Result
);

public sealed record YoloBatchResponseDto(
    [property: JsonPropertyName("results")]                 List<YoloBatchResultItemDto> Results,
    [property: JsonPropertyName("total_inference_time_ms")] double TotalInferenceTimeMs
);

// ── DINO Batch ──

public sealed record DinoBatchItemDto(
    [property: JsonPropertyName("image_base64")] string  ImageBase64,
    [property: JsonPropertyName("frame_id")]     string  FrameId    = "",
    [property: JsonPropertyName("text_prompt")]  string? TextPrompt = null
);

public sealed record DinoBatchRequestDto(
    [property: JsonPropertyName("items")]          List<DinoBatchItemDto> Items,
    [property: JsonPropertyName("box_threshold")]  double BoxThreshold  = 0.30,
    [property: JsonPropertyName("text_threshold")] double TextThreshold = 0.25
);

public sealed record DinoBatchResultItemDto(
    [property: JsonPropertyName("frame_id")] string FrameId,
    [property: JsonPropertyName("result")]   DinoResponse Result
);

public sealed record DinoBatchResponseDto(
    [property: JsonPropertyName("results")]                 List<DinoBatchResultItemDto> Results,
    [property: JsonPropertyName("total_inference_time_ms")] double TotalInferenceTimeMs
);

// ── SAM Batch ──

public sealed record SamBatchItemDto(
    [property: JsonPropertyName("image_base64")]   string ImageBase64,
    [property: JsonPropertyName("bounding_boxes")] List<SamBoundingBox> BoundingBoxes,
    [property: JsonPropertyName("frame_id")]        string FrameId       = "",
    [property: JsonPropertyName("pipe_diameter_mm")] int? PipeDiameterMm = null
);

public sealed record SamBatchRequestDto(
    [property: JsonPropertyName("items")] List<SamBatchItemDto> Items
);

public sealed record SamBatchResultItemDto(
    [property: JsonPropertyName("frame_id")] string FrameId,
    [property: JsonPropertyName("result")]   SamResponse Result
);

public sealed record SamBatchResponseDto(
    [property: JsonPropertyName("results")]                 List<SamBatchResultItemDto> Results,
    [property: JsonPropertyName("total_inference_time_ms")] double TotalInferenceTimeMs
);
