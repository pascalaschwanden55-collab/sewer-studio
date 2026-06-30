namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchUiSink
{
    public TrainingBatchUiSink(
        Action<bool> SetBusy,
        Action<string> SetLogText,
        Action<int> SetProgressValue,
        Action<int> SetProgressMax)
    {
        ArgumentNullException.ThrowIfNull(SetBusy);
        ArgumentNullException.ThrowIfNull(SetLogText);
        ArgumentNullException.ThrowIfNull(SetProgressValue);
        ArgumentNullException.ThrowIfNull(SetProgressMax);

        this.SetBusy = SetBusy;
        this.SetLogText = SetLogText;
        this.SetProgressValue = SetProgressValue;
        this.SetProgressMax = SetProgressMax;
    }

    public Action<bool> SetBusy { get; }
    public Action<string> SetLogText { get; }
    public Action<int> SetProgressValue { get; }
    public Action<int> SetProgressMax { get; }
}
