using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingSegmentedFindingVisibility
{
    public static IReadOnlyList<SegmentedFinding> BuildVisibleCodingFindings(
        IReadOnlyList<SegmentedFinding> segmented)
        => BuildVisibleMaskFindings(segmented)
            .Where(s => s.Proximity.IsCodierbar)
            .ToList();

    public static IReadOnlyList<SegmentedFinding> BuildVisibleMaskFindings(
        IReadOnlyList<SegmentedFinding> segmented)
        => segmented
            .Where(s => s.Proximity.IsCodierbar)
            .Where(IsMaskVisible)
            .ToList();

    public static string BuildOverlaySuppressionText(int suppressedBackgroundCount)
    {
        if (suppressedBackgroundCount <= 0)
            return "";

        return suppressedBackgroundCount == 1
            ? "1 Hintergrundmaske ausgeblendet"
            : $"{suppressedBackgroundCount} Hintergrundmasken ausgeblendet";
    }

    private static bool IsMaskVisible(SegmentedFinding segmented)
    {
        var candidate = new SamMaskRenderer.MaskRenderCandidate(
            segmented.Mask,
            segmented.Quant,
            segmented.Dino?.Confidence);
        var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);
        return decision.Mode != SamMaskRenderer.MaskVisualMode.Hidden;
    }
}
