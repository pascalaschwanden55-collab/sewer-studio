namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageAutoSaveController
{
    public static void Schedule(
        AutoSaveMode mode,
        Action markDirty,
        Action stopTimer,
        Action<TimeSpan> setInterval,
        Func<bool> isTimerEnabled,
        Action startTimer,
        Action save)
    {
        ArgumentNullException.ThrowIfNull(markDirty);
        ArgumentNullException.ThrowIfNull(stopTimer);
        ArgumentNullException.ThrowIfNull(setInterval);
        ArgumentNullException.ThrowIfNull(isTimerEnabled);
        ArgumentNullException.ThrowIfNull(startTimer);
        ArgumentNullException.ThrowIfNull(save);

        markDirty();

        var normalized = mode.Normalize();
        if (normalized == AutoSaveMode.OnEachChange)
        {
            stopTimer();
            save();
            return;
        }

        var interval = normalized.GetInterval();
        if (interval is null)
        {
            stopTimer();
            return;
        }

        setInterval(interval.Value);
        if (!isTimerEnabled())
            startTimer();
    }

    public static void HandleTimerTick(
        AutoSaveMode mode,
        Action save,
        Func<bool> isProjectDirty,
        Action stopTimer)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(isProjectDirty);
        ArgumentNullException.ThrowIfNull(stopTimer);

        if (mode.Normalize().GetInterval() is null)
        {
            stopTimer();
            return;
        }

        save();

        if (!isProjectDirty())
            stopTimer();
    }
}
