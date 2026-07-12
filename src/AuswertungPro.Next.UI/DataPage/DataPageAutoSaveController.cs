namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageAutoSaveController
{
    public static TimeSpan OnEachChangeDelay { get; } = TimeSpan.FromMilliseconds(750);

    public static void Schedule(
        AutoSaveMode mode,
        Action markDirty,
        Action stopTimer,
        Action<TimeSpan> setInterval,
        Func<bool> isTimerEnabled,
        Action startTimer)
    {
        ArgumentNullException.ThrowIfNull(markDirty);
        ArgumentNullException.ThrowIfNull(stopTimer);
        ArgumentNullException.ThrowIfNull(setInterval);
        ArgumentNullException.ThrowIfNull(isTimerEnabled);
        ArgumentNullException.ThrowIfNull(startTimer);

        markDirty();

        var normalized = mode.Normalize();
        if (normalized == AutoSaveMode.OnEachChange)
        {
            // Mehrere schnelle Eingaben zu genau einem Speicherlauf bündeln.
            // Stop + Start setzt die Wartezeit bei jeder neuen Änderung zurück.
            stopTimer();
            setInterval(OnEachChangeDelay);
            startTimer();
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

        var normalized = mode.Normalize();
        if (normalized == AutoSaveMode.OnEachChange)
        {
            stopTimer();
            save();
            return;
        }

        if (normalized.GetInterval() is null)
        {
            stopTimer();
            return;
        }

        save();

        if (!isProjectDirty())
            stopTimer();
    }
}
