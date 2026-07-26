using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingLiveAiTickPolicy
{
    public static bool ShouldAnalyze(
        bool isClosing,
        bool hasPlayer,
        bool hasLiveDetection,
        CodingSessionState? sessionState,
        bool isPlayerPlaying)
    {
        if (isClosing || !hasPlayer || !hasLiveDetection)
            return false;

        if (sessionState == CodingSessionState.WaitingForUserInput)
            return false;

        return isPlayerPlaying;
    }
}
