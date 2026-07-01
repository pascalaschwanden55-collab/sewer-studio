namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchUiSink
{
    public TrainingBatchUiSink(
        Action<bool> setBusy,
        Action<string> setLogText,
        Action<int> setProgressValue,
        Action<int> setProgressMax,
        Action<string> setStatusText,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(setBusy);
        ArgumentNullException.ThrowIfNull(setLogText);
        ArgumentNullException.ThrowIfNull(setProgressValue);
        ArgumentNullException.ThrowIfNull(setProgressMax);
        ArgumentNullException.ThrowIfNull(setStatusText);
        ArgumentNullException.ThrowIfNull(log);

        SetBusy = setBusy;
        SetLogText = setLogText;
        SetProgressValue = setProgressValue;
        SetProgressMax = setProgressMax;
        SetStatusText = setStatusText;
        Log = log;
    }

    public Action<bool> SetBusy { get; }

    public Action<string> SetLogText { get; }

    public Action<int> SetProgressValue { get; }

    public Action<int> SetProgressMax { get; }

    public Action<string> SetStatusText { get; }

    public Action<string> Log { get; }
}
