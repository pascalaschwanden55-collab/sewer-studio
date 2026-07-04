namespace AuswertungPro.Next.UI.Ai;

public enum VsaCodeExplorerProgressBarRole
{
    Success,
    Group,
    CurrentGroup,
    BorderLight
}

public enum VsaCodeExplorerProgressLabelRole
{
    Secondary,
    Muted
}

public sealed record VsaCodeExplorerProgressPresentation(
    IReadOnlyList<VsaCodeExplorerProgressSegmentPresentation> Segments,
    string CodePreviewText);

public sealed record VsaCodeExplorerProgressSegmentPresentation(
    VsaCodeExplorerProgressBarRole BarRole,
    bool LabelBold,
    VsaCodeExplorerProgressLabelRole LabelRole);

public static class VsaCodeExplorerProgressPresenter
{
    private const int SegmentCount = 4;

    public static VsaCodeExplorerProgressPresentation Build(
        int currentLevel,
        bool showResultPanel,
        string? finalCode)
    {
        var segments = new List<VsaCodeExplorerProgressSegmentPresentation>(SegmentCount);

        for (var i = 0; i < SegmentCount; i++)
        {
            segments.Add(new VsaCodeExplorerProgressSegmentPresentation(
                BarRole: ResolveBarRole(i, currentLevel, showResultPanel),
                LabelBold: i == currentLevel && !showResultPanel,
                LabelRole: i <= currentLevel || showResultPanel
                    ? VsaCodeExplorerProgressLabelRole.Secondary
                    : VsaCodeExplorerProgressLabelRole.Muted));
        }

        return new VsaCodeExplorerProgressPresentation(segments, finalCode ?? "");
    }

    private static VsaCodeExplorerProgressBarRole ResolveBarRole(
        int segmentIndex,
        int currentLevel,
        bool showResultPanel)
    {
        if (showResultPanel && segmentIndex >= currentLevel)
            return VsaCodeExplorerProgressBarRole.Success;

        if (segmentIndex < currentLevel)
            return VsaCodeExplorerProgressBarRole.Group;

        if (segmentIndex == currentLevel)
            return VsaCodeExplorerProgressBarRole.CurrentGroup;

        return VsaCodeExplorerProgressBarRole.BorderLight;
    }
}
