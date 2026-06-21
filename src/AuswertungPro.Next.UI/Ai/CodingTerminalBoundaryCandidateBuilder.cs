using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingTerminalBoundaryCandidateBuilder
{
    public static IEnumerable<(string? Code, double? Meter, TimeSpan? VideoTime)> Enumerate(
        IEnumerable<CodingEvent>? sessionEvents,
        IEnumerable<CodingEvent>? uiEvents,
        IEnumerable<CodingEvent>? importEvents)
    {
        foreach (var ev in sessionEvents ?? [])
            yield return ToCandidate(ev);

        foreach (var ev in uiEvents ?? [])
            yield return ToCandidate(ev);

        foreach (var ev in importEvents ?? [])
            yield return ToCandidate(ev);
    }

    public static (string? Code, double? Meter, TimeSpan? VideoTime) ToCandidate(CodingEvent ev)
    {
        var meter = ev.Entry.MeterStart ?? (ev.MeterAtCapture > 0 ? ev.MeterAtCapture : null);
        var videoTime = ev.Entry.Zeit ?? (ev.VideoTimestamp > TimeSpan.Zero ? ev.VideoTimestamp : null);
        return (ev.Entry.Code, meter, videoTime);
    }
}
