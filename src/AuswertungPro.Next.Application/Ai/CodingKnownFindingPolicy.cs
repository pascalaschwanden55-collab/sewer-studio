using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

public static class CodingKnownFindingPolicy
{
    public static bool IsKnown(
        LiveFrameFinding finding,
        double meter,
        IEnumerable<CodingEvent>? sessionEvents,
        IEnumerable<CodingEvent>? viewEvents)
    {
        var code = finding.VsaCodeHint ?? string.Empty;
        if (string.IsNullOrEmpty(code))
            return false;

        return IsCoveredIn(sessionEvents, code, meter, finding)
            || IsCoveredIn(viewEvents, code, meter, finding);
    }

    private static bool IsCoveredIn(
        IEnumerable<CodingEvent>? events,
        string code,
        double meter,
        LiveFrameFinding finding)
    {
        if (events == null)
            return false;

        foreach (var existing in events)
        {
            if (!CodingDedupPolicy.CodesMatch(existing.Entry.Code, code))
                continue;
            if (CodingFindingCoveragePolicy.IsCovered(existing, meter, finding))
                return true;
        }

        return false;
    }
}
