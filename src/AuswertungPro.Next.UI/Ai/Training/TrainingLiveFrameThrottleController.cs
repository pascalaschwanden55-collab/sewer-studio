namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingLiveFrameThrottleDecision(
    bool ShouldUpdateFramePath,
    string? FramePath,
    DateTime LastUpdatedUtc);

public static class TrainingLiveFrameThrottleController
{
    private const double MinFrameUpdateIntervalMilliseconds = 180;

    public static TrainingLiveFrameThrottleDecision Decide(
        string? framePath,
        DateTime lastUpdatedUtc,
        DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(framePath))
        {
            return new TrainingLiveFrameThrottleDecision(
                ShouldUpdateFramePath: true,
                FramePath: "",
                LastUpdatedUtc: lastUpdatedUtc);
        }

        if ((nowUtc - lastUpdatedUtc).TotalMilliseconds < MinFrameUpdateIntervalMilliseconds)
        {
            return new TrainingLiveFrameThrottleDecision(
                ShouldUpdateFramePath: false,
                FramePath: null,
                LastUpdatedUtc: lastUpdatedUtc);
        }

        return new TrainingLiveFrameThrottleDecision(
            ShouldUpdateFramePath: true,
            FramePath: framePath,
            LastUpdatedUtc: nowUtc);
    }
}
