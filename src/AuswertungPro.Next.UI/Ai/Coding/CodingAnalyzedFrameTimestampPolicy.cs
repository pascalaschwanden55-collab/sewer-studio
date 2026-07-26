namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingAnalyzedFrameTimestampPolicy
{
    public static double? Resolve(double? pendingTimestampSeconds, double? firstCleanFrameSeconds)
    {
        if (firstCleanFrameSeconds.HasValue
            && (!pendingTimestampSeconds.HasValue || pendingTimestampSeconds.Value < firstCleanFrameSeconds.Value))
        {
            return firstCleanFrameSeconds.Value;
        }

        return pendingTimestampSeconds;
    }
}
