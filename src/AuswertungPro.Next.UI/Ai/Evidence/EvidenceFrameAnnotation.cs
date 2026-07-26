namespace AuswertungPro.Next.UI.Ai.Evidence;

public sealed record EvidenceFrameAnnotation(
    string Code,
    double? Confidence,
    double? BboxXCenter,
    double? BboxYCenter,
    double? BboxWidth,
    double? BboxHeight,
    string? MaskRle = null,
    int? MaskImageWidth = null,
    int? MaskImageHeight = null)
{
    public bool HasBbox =>
        BboxXCenter.HasValue
        && BboxYCenter.HasValue
        && BboxWidth.HasValue
        && BboxHeight.HasValue
        && BboxWidth.Value > 0
        && BboxHeight.Value > 0;

    public bool HasMask =>
        !string.IsNullOrWhiteSpace(MaskRle)
        && MaskImageWidth is > 0
        && MaskImageHeight is > 0;
}
