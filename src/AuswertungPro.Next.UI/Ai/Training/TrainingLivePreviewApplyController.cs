namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingLivePreviewApplyUi(
    Action<string> SetLiveCaseInfo,
    Action<string> SetLiveCodeInfo,
    Action<string> SetLiveMeterInfo,
    Action<string> SetCurrentComparisonText,
    Action<string> SetCurrentEntryCode,
    Action<string> SetLiveFrameThrottled,
    Func<string> GetLiveFramePath,
    Action<string> SetLiveFramePath);

public static class TrainingLivePreviewApplyController
{
    public static void ApplyOnUi(
        string caseInfo,
        string code,
        string meter,
        string? framePath,
        TrainingLivePreviewApplyUi ui,
        Action<Action> onUi)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(onUi);

        onUi(() =>
        {
            var preview = TrainingLivePreviewPresenter.Build(caseInfo, code, meter, framePath);
            Apply(preview, ui);
        });
    }

    public static void Apply(TrainingLivePreview preview, TrainingLivePreviewApplyUi ui)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(ui);

        ui.SetLiveCaseInfo(preview.LiveCaseInfo);
        ui.SetLiveCodeInfo(preview.LiveCodeInfo);
        ui.SetLiveMeterInfo(preview.LiveMeterInfo);
        ui.SetCurrentComparisonText(preview.CurrentComparisonText);
        ui.SetCurrentEntryCode(preview.CurrentEntryCode);

        if (preview.FramePath is not null)
        {
            ui.SetLiveFrameThrottled(preview.FramePath);
            return;
        }

        if (string.IsNullOrEmpty(ui.GetLiveFramePath()))
            ui.SetLiveFramePath("");
    }
}
