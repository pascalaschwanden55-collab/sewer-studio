using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

public static class LiveDetectionConfirmationPolicy
{
    public const int MinimumConfirmationSeverity = 2;

    public static List<LiveFrameFinding> SelectSignificantFindings(
        IEnumerable<LiveFrameFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return findings
            .Where(f => f.Severity >= MinimumConfirmationSeverity)
            .ToList();
    }
}
