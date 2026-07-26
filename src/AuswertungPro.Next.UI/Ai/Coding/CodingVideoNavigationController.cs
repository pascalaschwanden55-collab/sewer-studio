using System;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingVideoNavigationController
{
    public static double ResolveDisplayMeter(
        double? osdMeter,
        long playerTimeMs,
        long playerLengthMs,
        double endMeter,
        double sessionCurrentMeter)
        => CodingCurrentMeterResolver.Resolve(
            osdMeter,
            playerTimeMs,
            playerLengthMs,
            endMeter,
            sessionCurrentMeter);

    public static bool SyncVideoToCodingMeter(
        double currentMeter,
        double endMeter,
        long playerLengthMs,
        Action<long> setPlayerTimeMs,
        Func<long> getPlayerTimeMs,
        Action<TimeSpan> setCurrentVideoTime)
    {
        if (!CodingVideoSyncPolicy.TryResolveTargetTimeMs(
                currentMeter,
                endMeter,
                playerLengthMs,
                out var targetMs))
            return false;

        setPlayerTimeMs(targetMs);
        setCurrentVideoTime(TimeSpan.FromMilliseconds(getPlayerTimeMs()));
        return true;
    }

    public static bool PrepareMoveByCommand<TViewModel>(
        TViewModel? viewModel,
        Action<TViewModel> executeMoveCommand,
        Action markNavigationPending,
        Action pausePlayback,
        Action resetRecentOsdTracking)
        where TViewModel : class
    {
        if (viewModel == null)
            return false;

        markNavigationPending();
        executeMoveCommand(viewModel);
        pausePlayback();
        resetRecentOsdTracking();
        return true;
    }
}
