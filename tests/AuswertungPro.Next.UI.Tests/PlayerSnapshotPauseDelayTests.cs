using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerSnapshotPauseDelayTests
{
    [Fact]
    public void WaitAfterPause_uses_standard_delay()
    {
        TimeSpan? capturedDelay = null;

        PlayerSnapshotPauseDelay.WaitAfterPause(delay => capturedDelay = delay);

        Assert.Equal(TimeSpan.FromMilliseconds(60), capturedDelay);
    }
}
