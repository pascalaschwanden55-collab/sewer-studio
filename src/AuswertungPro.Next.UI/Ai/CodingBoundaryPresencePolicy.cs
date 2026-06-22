using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingBoundaryPresence(int ViewCount, int SessionCount)
{
    public bool Exists => ViewCount > 0 || SessionCount > 0;
}

public static class CodingBoundaryPresencePolicy
{
    public static CodingBoundaryPresence CountExisting(
        IEnumerable<CodingEvent>? viewEvents,
        IEnumerable<CodingEvent>? sessionEvents,
        string code)
        => new(
            CountMatchingCode(viewEvents, code),
            CountMatchingCode(sessionEvents, code));

    public static bool ExistsInView(IEnumerable<CodingEvent>? viewEvents, string code)
        => CountMatchingCode(viewEvents, code) > 0;

    private static int CountMatchingCode(IEnumerable<CodingEvent>? events, string code)
        => events?.Count(e => string.Equals(e.Entry.Code, code, StringComparison.OrdinalIgnoreCase)) ?? 0;
}
