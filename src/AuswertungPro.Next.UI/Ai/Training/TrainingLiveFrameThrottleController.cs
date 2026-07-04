namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingLiveFrameThrottleDecision(
    bool ShouldUpdateFramePath,
    string? FramePath,
    DateTime LastUpdatedUtc);

public static class TrainingLiveFrameThrottleController
{
    private const double MinFrameUpdateIntervalMilliseconds = 180;

    public static void Apply(
        string? framePath,
        Func<DateTime> getLastUpdatedUtc,
        Action<DateTime> setLastUpdatedUtc,
        Action<string> setFramePath)
        => Apply(
            framePath,
            getLastUpdatedUtc,
            setLastUpdatedUtc,
            setFramePath,
            () => DateTime.UtcNow);

    public static void Apply(
        string? framePath,
        Func<DateTime> getLastUpdatedUtc,
        Action<DateTime> setLastUpdatedUtc,
        Action<string> setFramePath,
        Func<DateTime> getNowUtc)
    {
        ArgumentNullException.ThrowIfNull(getLastUpdatedUtc);
        ArgumentNullException.ThrowIfNull(setLastUpdatedUtc);
        ArgumentNullException.ThrowIfNull(setFramePath);
        ArgumentNullException.ThrowIfNull(getNowUtc);

        var decision = Decide(framePath, getLastUpdatedUtc(), getNowUtc());
        if (!decision.ShouldUpdateFramePath)
            return;

        setFramePath(decision.FramePath ?? "");
        setLastUpdatedUtc(decision.LastUpdatedUtc);
    }

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
