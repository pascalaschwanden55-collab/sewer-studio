namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingUiSink
{
    public SelfTrainingUiSink(
        Action<bool> setBusy,
        Action<bool> setSelfTrainingRunning,
        Action<string> setLogText,
        Action<string> setStatusText,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(setBusy);
        ArgumentNullException.ThrowIfNull(setSelfTrainingRunning);
        ArgumentNullException.ThrowIfNull(setLogText);
        ArgumentNullException.ThrowIfNull(setStatusText);
        ArgumentNullException.ThrowIfNull(log);

        SetBusy = setBusy;
        SetSelfTrainingRunning = setSelfTrainingRunning;
        SetLogText = setLogText;
        SetStatusText = setStatusText;
        Log = log;
    }

    public Action<bool> SetBusy { get; }

    public Action<bool> SetSelfTrainingRunning { get; }

    public Action<string> SetLogText { get; }

    public Action<string> SetStatusText { get; }

    public Action<string> Log { get; }
}
