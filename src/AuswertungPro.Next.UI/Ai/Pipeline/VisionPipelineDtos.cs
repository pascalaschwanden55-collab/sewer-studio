using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

// ── Health ─────────────────────────────────────────────────────────────────

public sealed record SidecarHealthResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("gpu")] GpuStatus? Gpu
);

public sealed record GpuStatus(
    [property: JsonPropertyName("current_model")] string CurrentModel,
    [property: JsonPropertyName("vram_allocated_gb")] double VramAllocatedGb,
    [property: JsonPropertyName("vram_total_gb")] double VramTotalGb
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
    [property: JsonPropertyName("inference_time_ms")] double InferenceTimeMs
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
