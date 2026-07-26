using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingNewFindingOverlaySelector
{
    public static IReadOnlyList<LiveFrameFinding> Select(
        IEnumerable<LiveFrameFinding> findings,
        double currentMeter,
        Func<LiveFrameFinding, double, bool> isKnown)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(isKnown);

        return findings
            .Where(finding => !isKnown(finding, currentMeter))
            .ToList();
    }
}
