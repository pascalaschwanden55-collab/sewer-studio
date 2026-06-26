using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStreckenschadenTrackerOwnerTests
{
    [Fact]
    public void Owner_delegates_update_close_all_and_reset()
    {
        var owner = new CodingStreckenschadenTrackerOwner();
        var observations = new[]
        {
            new StreckenschadenTracker.Observation("BBA", 3, 12.5)
        };

        var updateActions = owner.Update(observations, currentMeter: 12.5);
        var closeActions = owner.CloseAll(currentMeter: 14.0);
        owner.Reset();

        Assert.Single(updateActions);
        Assert.Equal(StreckenschadenTracker.SegmentActionType.Open, updateActions[0].Type);
        Assert.Single(closeActions);
        Assert.Equal(StreckenschadenTracker.SegmentActionType.Close, closeActions[0].Type);
        Assert.Equal(0, owner.OpenCount);
    }
}
