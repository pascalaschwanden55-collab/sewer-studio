using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingSessionEventResetter
{
    public static int ClearActiveSessionEvents(ICodingSessionService? sessionService)
    {
        var events = sessionService?.ActiveSession?.Events;
        if (events == null || events.Count == 0)
            return 0;

        var removed = events.Count;
        events.Clear();
        return removed;
    }
}
