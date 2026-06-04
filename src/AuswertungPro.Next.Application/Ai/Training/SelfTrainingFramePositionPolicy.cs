namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Bewertet, ob ein Self-Training-Frame verlaesslich an die Protokollposition gebunden ist.
/// PdfPhoto und VideoTimestamp sind stabil; VideoLinear ist nur geschaetzt.
/// </summary>
public static class SelfTrainingFramePositionPolicy
{
    public static bool IsReliable(bool usedVideoFallback, bool hasProtocolTimestamp)
        => !usedVideoFallback || hasProtocolTimestamp;
}
