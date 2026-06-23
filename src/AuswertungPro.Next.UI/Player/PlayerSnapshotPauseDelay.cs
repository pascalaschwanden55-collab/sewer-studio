using System;
using System.Threading;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerSnapshotPauseDelay
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(60);

    public static void WaitAfterPause(Action<TimeSpan>? sleep = null)
        => (sleep ?? Thread.Sleep)(Delay);
}
