using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingTerminalBoundaryPresencePolicy
{
    public static bool HasEndOrAbortCode(IEnumerable<CodingEvent>? events)
        => events?.Any(e => MainCode(e.Entry.Code) is "BCE" or "BDC") ?? false;

    private static string? MainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var trimmed = code.Trim();
        return trimmed.Length >= 3
            ? trimmed[..3].ToUpperInvariant()
            : trimmed.ToUpperInvariant();
    }
}
