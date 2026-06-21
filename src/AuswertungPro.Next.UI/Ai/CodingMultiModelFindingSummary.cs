using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingMultiModelFindingSummary(
    int TotalCount,
    int VorausCount,
    int CodierbarCount,
    int SuppressedBackgroundCount,
    string OverlaySuppressionText,
    string TimingText,
    IReadOnlyList<SegmentedFinding> VisibleCodierbar)
{
    public bool HasNoSegmentedFindings => TotalCount == 0;

    public bool HasOnlyAheadFindings => CodierbarCount == 0 && VorausCount > 0;

    public string DetectedStatusText =>
        $"{CodierbarCount} Befunde erkannt" + (VorausCount > 0 ? $" ({VorausCount} voraus ignoriert)" : "");

    public static CodingMultiModelFindingSummary Build(
        IReadOnlyList<SegmentedFinding> segmented,
        SingleFrameResult result)
    {
        var vorausCount = segmented.Count(s => !s.Proximity.IsCodierbar);
        var codierbarCount = segmented.Count - vorausCount;
        var visibleCodierbar = CodingSegmentedFindingVisibility.BuildVisibleCodingFindings(segmented);
        var suppressedBackgroundCount = segmented.Count(s => s.Proximity.IsCodierbar) - visibleCodierbar.Count;
        var overlaySuppressionText = CodingSegmentedFindingVisibility.BuildOverlaySuppressionText(suppressedBackgroundCount);

        var timingText = $"YOLO {result.YoloTimeMs:F0}ms | DINO {result.DinoTimeMs:F0}ms | SAM {result.SamTimeMs:F0}ms";
        if (!string.IsNullOrEmpty(overlaySuppressionText))
            timingText += $" | {overlaySuppressionText}";

        return new CodingMultiModelFindingSummary(
            segmented.Count,
            vorausCount,
            codierbarCount,
            suppressedBackgroundCount,
            overlaySuppressionText,
            timingText,
            visibleCodierbar);
    }
}
