using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingWarmupResultSelection(
    LiveDetection Result,
    bool ShouldClearPending);

public static class CodingWarmupResultBufferPolicy
{
    public static CodingWarmupResultSelection Select(LiveDetection current, LiveDetection? pending)
    {
        if (pending is null)
            return new CodingWarmupResultSelection(current, ShouldClearPending: false);

        var selected = current.Findings.Count == 0 && pending.Findings.Count > 0
            ? pending
            : current;

        return new CodingWarmupResultSelection(selected, ShouldClearPending: true);
    }
}
