using System;
using System.Windows.Threading;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerWindowTimerStopper
{
    public static void StopPlaybackTimers(
        DispatcherTimer updateTimer,
        DispatcherTimer scrubTimer,
        DispatcherTimer? detectionTimer,
        CodingLiveAiTimerController? codingLiveAiTimers,
        DispatcherTimer? codingOsdTimer)
    {
        StopTimer(updateTimer);
        StopTimer(scrubTimer);
        StopTimer(detectionTimer);
        TryStop(() => codingLiveAiTimers?.StopTimers());
        StopTimer(codingOsdTimer);
    }

    private static void StopTimer(DispatcherTimer? timer)
        => TryStop(() => timer?.Stop());

    private static void TryStop(Action stop)
    {
        try { stop(); } catch { }
    }
}
