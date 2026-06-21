using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingOneTimeCodeDuplicatePolicy
{
    public static bool AlreadyExists(
        string? code,
        IEnumerable<CodingEvent>? sessionEvents,
        IEnumerable<CodingEvent>? viewEvents)
    {
        if (!CodingDedupPolicy.IsOneTimeCode(code))
            return false;

        return ContainsMatchingCode(sessionEvents, code)
            || ContainsMatchingCode(viewEvents, code);
    }

    private static bool ContainsMatchingCode(IEnumerable<CodingEvent>? events, string? code)
        => events?.Any(e => CodingDedupPolicy.CodesMatch(e.Entry.Code, code)) == true;
}
