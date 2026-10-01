using AuswertungPro.Next.Application.Ai;

namespace CodingReplay;

/// <summary>Beobachtet echte Antworten, ohne Filter oder Modellfreigaben zu aendern.</summary>
public sealed class ReplayVisionClient(IVisionPipelineClient inner) : IVisionPipelineClient
{
    public Dictionary<string, string> Trace { get; } = new();
    public string? TechnicalError { get; private set; }
    public void Reset() { Trace.Clear(); TechnicalError = null; }
    public Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default) => inner.HealthCheckAsync(ct);
    public Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default) => inner.CheckHealthDetailedAsync(ct);
    public async Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
    {
        Trace["classifier_called"] = "true";
        try
        {
            var r = await inner.ClassifyYoloAsync(request, ct);
            Trace["classifier_loaded"] = r.ClassifierLoaded.ToString();
            Trace["classifier_usable"] = r.Usable.ToString();
            Trace["classifier_model"] = r.ModelName;
            Trace["classifier_sha256"] = r.ModelSha256;
            Trace["classifier_preprocessing"] = r.Preprocessing;
            Trace["classifier_imgsz"] = r.Imgsz.ToString();
            Trace["classifier_predictions"] = string.Join("; ", r.Predictions.Select(p => $"{p.ClassName}: {p.Confidence:F3}"));
            return r;
        }
        catch (Exception) { Trace["classifier_failed"] = "true"; throw; }
    }
    public async Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
    {
        Trace["yolo_called"] = "true";
        var r = await inner.DetectYoloAsync(request, ct);
        Trace["yolo_boxes"] = r.Detections.Count.ToString();
        Trace["yolo_model"] = r.ModelName ?? "";
        Trace["yolo_backend"] = r.ModelBackend ?? "";
        Trace["yolo_artifact_sha256"] = r.DetectorArtifactSha256 ?? "";
        Trace["yolo_qualified"] = r.DetectorQualified?.ToString() ?? "unknown";
        Trace["yolo_qualification_status"] = r.DetectorQualificationStatus;
        Trace["yolo_qualification_reason"] = r.DetectorQualificationReason ?? "";
        Trace["yolo_detections"] = string.Join("; ", r.Detections.Select(d =>
            $"{d.ClassName}:{d.Confidence:F3}:[{d.X1:F1},{d.Y1:F1},{d.X2:F1},{d.Y2:F1}]"));
        return r;
    }
    public async Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
    {
        Trace["dino_called"] = "true";
        Trace["dino_prompt"] = request.TextPrompt ?? "sidecar_default";
        var r = await inner.DetectDinoAsync(request, ct);
        Trace["dino_boxes"] = r.Detections.Count.ToString();
        Trace["dino_detections"] = string.Join("; ", r.Detections.Select(d =>
            $"{d.Label}:{d.Confidence:F3}:[{d.X1:F1},{d.Y1:F1},{d.X2:F1},{d.Y2:F1}]"));
        if (r.Degraded || !string.IsNullOrWhiteSpace(r.Error)) TechnicalError = "DINO: " + (r.Error ?? "unvollstaendig");
        return r;
    }
    public async Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
    {
        Trace["sam_called"] = "true";
        var r = await inner.SegmentSamAsync(request, ct);
        Trace["sam_masks"] = r.Masks.Count.ToString();
        Trace["sam_skipped"] = r.SkippedBoxes.ToString();
        if (!string.IsNullOrWhiteSpace(r.Error) || (r.Degraded && r.SkippedBoxes == 0) || r.SkippedBoxes > r.LowScoreBoxes)
            TechnicalError = "SAM: " + (r.Error ?? "unvollstaendig");
        return r;
    }
}
