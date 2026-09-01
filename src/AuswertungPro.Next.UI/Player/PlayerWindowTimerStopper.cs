using System;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Common;

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

    public static DispatcherTimer? StopAndClear(DispatcherTimer? timer)
    {
        StopTimer(timer);
        return null;
    }

    private static void StopTimer(DispatcherTimer? timer)
        => TryStop(() => timer?.Stop());

    private static void TryStop(Action stop)
    {
        try
        {
            stop();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[Player] Wiedergabe-Timer konnte nicht gestoppt werden: {ex}");
        }
    }
}
