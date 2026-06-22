using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class AiFindingDisplayItemFactory
{
    public static IReadOnlyList<AiFindingDisplayItem> ForPossibleBoundary(string? code, string label)
        => Single(CodingClassifierDisplayPolicy.BuildPossibleBoundaryFinding(code, label));

    public static IReadOnlyList<AiFindingDisplayItem> ForBoundary(string? code, string label)
        => Single(CodingClassifierDisplayPolicy.BuildBoundaryFinding(code, label));

    public static IReadOnlyList<AiFindingDisplayItem> ForResolvedFinding(
        LiveFrameFinding finding,
        string resolvedCode)
        => Single(finding with { VsaCodeHint = resolvedCode });

    private static IReadOnlyList<AiFindingDisplayItem> Single(LiveFrameFinding finding)
        => [new AiFindingDisplayItem(finding)];
}
