namespace AuswertungPro.Next.Application.Ai;

/// <summary>Schreibt Frame-Ablauf und Zusammenfassung eines KI-Laufs.</summary>
public interface IPipelineTraceWriter
{
    Task WriteAsync(PipelineTraceEntry entry);

    Task WriteSummaryAsync(string runId, TelemetrySummary summary);

    string? ResolvePath(string runId);

    string? ResolveSummaryPath(string runId);
}

public sealed class PipelineTraceEntry
{
    public string RunId { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; }
    public int FrameIndex { get; set; }
    public double TimeSec { get; set; }
    public double Meter { get; set; }
    public string Path { get; set; } = "processed";
    public bool YoloBypass { get; set; }
    public bool? YoloRelevant { get; set; }
    public int YoloDetectionCount { get; set; }
    public int DinoBoxCount { get; set; }
    public int SamMaskCount { get; set; }
    public int FindingsBuilt { get; set; }
    public int CodesFromLabel { get; set; }
    public string? ClassifierCode { get; set; }
    public double? ClassifierConfidence { get; set; }
    public string? ClassifierSource { get; set; }
    public string? ClassifierModel { get; set; }
    public bool? ClassifierVoteConfirmed { get; set; }
    public bool QwenCalled { get; set; }
    public string? QwenImageQuality { get; set; }
    public int QwenRawFindingCount { get; set; }
    public int CodesAfterQwen { get; set; }
    public int FindingsEndOfFrame { get; set; }
    public int ActiveCount { get; set; }
    public int DetectionsTotal { get; set; }
    public string? DropReason { get; set; }
    public bool Degraded { get; set; }
    public string? DegradedReason { get; set; }
}
