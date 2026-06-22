namespace AuswertungPro.Next.UI.Ai;

public static class CodingOsdTimerPolicy
{
    public static bool ShouldReadMeter(
        bool isClosing,
        bool hasPlayer,
        bool isCodingMode,
        bool isOsdReading,
        bool isAnalyzing,
        bool hasLiveDetection)
    {
        if (isClosing || !hasPlayer)
            return false;

        return isCodingMode
               && !isOsdReading
               && !isAnalyzing
               && hasLiveDetection;
    }
}
