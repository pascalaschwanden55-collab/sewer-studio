using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingStreckenschadenTrackerOwner
{
    private readonly StreckenschadenTracker _tracker = new();

    public int OpenCount => _tracker.OpenCount;

    public IReadOnlyList<StreckenschadenTracker.SegmentAction> Update(
        IReadOnlyList<StreckenschadenTracker.Observation> observations,
        double currentMeter)
        => _tracker.Update(observations, currentMeter);

    public IReadOnlyList<StreckenschadenTracker.SegmentAction> CloseAll(double currentMeter)
        => _tracker.CloseAll(currentMeter);

    public void Reset()
    {
        _tracker.Reset();
    }
}
