using System;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingVideoSyncPolicy
{
    public static bool TryResolveTargetTimeMs(
        double currentMeter,
        double endMeter,
        long playerLengthMs,
        out long targetTimeMs)
    {
        targetTimeMs = 0;
        if (endMeter <= 0 || playerLengthMs <= 0)
            return false;

        var fraction = currentMeter / endMeter;
        var unclamped = (long)(fraction * playerLengthMs);
        targetTimeMs = Math.Clamp(unclamped, 0, playerLengthMs);
        return true;
    }
}
