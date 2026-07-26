namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingFrameReadinessState
{
    WaitingForVideo,
    Warmup,
    Ready
}

public sealed class CodingFrameReadinessTracker
{
    public CodingFrameReadinessState State { get; private set; } = CodingFrameReadinessState.WaitingForVideo;
    public int SkippedFrames { get; private set; }
    public int MeterConfirmCount { get; private set; }
    public double? FirstCleanFrameSeconds { get; private set; }
    public bool IsReady => State == CodingFrameReadinessState.Ready;

    public void Reset()
    {
        State = CodingFrameReadinessState.WaitingForVideo;
        SkippedFrames = 0;
        MeterConfirmCount = 0;
        FirstCleanFrameSeconds = null;
    }

    public void Update(double? frameTimestampSeconds, bool hasMeterThisFrame, double fallbackTimestampSeconds)
    {
        if (State == CodingFrameReadinessState.Ready)
            return;

        switch (State)
        {
            case CodingFrameReadinessState.WaitingForVideo:
                if (hasMeterThisFrame)
                {
                    State = CodingFrameReadinessState.Warmup;
                    MeterConfirmCount = 1;
                    SkippedFrames = 0;
                }
                else
                {
                    SkippedFrames++;
                    if (SkippedFrames >= 3)
                        MarkReady(frameTimestampSeconds, fallbackTimestampSeconds);
                }
                break;

            case CodingFrameReadinessState.Warmup:
                if (hasMeterThisFrame)
                    MeterConfirmCount++;

                if (MeterConfirmCount >= 2)
                {
                    MeterConfirmCount = 0;
                    MarkReady(frameTimestampSeconds, fallbackTimestampSeconds);
                }
                else
                {
                    SkippedFrames++;
                    if (SkippedFrames >= 2)
                    {
                        MeterConfirmCount = 0;
                        MarkReady(frameTimestampSeconds, fallbackTimestampSeconds);
                    }
                }
                break;
        }
    }

    private void MarkReady(double? frameTimestampSeconds, double fallbackTimestampSeconds)
    {
        State = CodingFrameReadinessState.Ready;
        FirstCleanFrameSeconds ??= frameTimestampSeconds is >= 0
            ? frameTimestampSeconds.Value
            : Math.Max(0.0, fallbackTimestampSeconds);
    }
}
