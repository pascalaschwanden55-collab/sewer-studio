namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingLivePreviewClearController
{
    public static void ApplyOnUi(
        Action<string> setLiveCaseInfo,
        Action<string> setLiveCodeInfo,
        Action<string> setLiveMeterInfo,
        Action<string> setCurrentComparisonText,
        Action<string> setCurrentEntryCode,
        Action<string> setLiveFramePath,
        Action<Action> onUi)
    {
        ArgumentNullException.ThrowIfNull(onUi);

        onUi(() => Apply(
            setLiveCaseInfo,
            setLiveCodeInfo,
            setLiveMeterInfo,
            setCurrentComparisonText,
            setCurrentEntryCode,
            setLiveFramePath));
    }

    public static void Apply(
        Action<string> setLiveCaseInfo,
        Action<string> setLiveCodeInfo,
        Action<string> setLiveMeterInfo,
        Action<string> setCurrentComparisonText,
        Action<string> setCurrentEntryCode,
        Action<string> setLiveFramePath)
    {
        ArgumentNullException.ThrowIfNull(setLiveCaseInfo);
        ArgumentNullException.ThrowIfNull(setLiveCodeInfo);
        ArgumentNullException.ThrowIfNull(setLiveMeterInfo);
        ArgumentNullException.ThrowIfNull(setCurrentComparisonText);
        ArgumentNullException.ThrowIfNull(setCurrentEntryCode);
        ArgumentNullException.ThrowIfNull(setLiveFramePath);

        var preview = TrainingLivePreviewPresenter.Clear();
        setLiveCaseInfo(preview.LiveCaseInfo);
        setLiveCodeInfo(preview.LiveCodeInfo);
        setLiveMeterInfo(preview.LiveMeterInfo);
        setCurrentComparisonText(preview.CurrentComparisonText);
        setCurrentEntryCode(preview.CurrentEntryCode);
        setLiveFramePath(preview.FramePath ?? "");
    }
}
