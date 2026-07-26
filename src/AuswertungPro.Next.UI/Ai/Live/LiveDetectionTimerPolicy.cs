namespace AuswertungPro.Next.UI.Ai.Live;

public static class LiveDetectionTimerPolicy
{
    public static bool ShouldRunTick(
        bool isClosing,
        bool hasPlayer,
        bool isDetectionInFlight,
        bool hasLiveDetectionService,
        bool hasDetectionCancellation,
        bool isPlayerPlaying,
        bool hasPendingFindings)
    {
        if (isClosing || !hasPlayer)
            return false;

        return !isDetectionInFlight
               && hasLiveDetectionService
               && hasDetectionCancellation
               && isPlayerPlaying
               && !hasPendingFindings;
    }
}
