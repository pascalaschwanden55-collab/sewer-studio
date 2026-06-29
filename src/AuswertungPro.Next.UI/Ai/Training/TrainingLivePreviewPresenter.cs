namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingLivePreview(
    string LiveCaseInfo,
    string LiveCodeInfo,
    string LiveMeterInfo,
    string CurrentComparisonText,
    string CurrentEntryCode,
    string? FramePath);

public static class TrainingLivePreviewPresenter
{
    public static TrainingLivePreview Build(
        string caseInfo,
        string code,
        string meter,
        string? framePath)
    {
        return new TrainingLivePreview(
            caseInfo,
            code,
            meter,
            $"{code} @ {meter}",
            code,
            framePath);
    }

    public static TrainingLivePreview Clear()
    {
        return new TrainingLivePreview("", "", "", "", "", "");
    }
}
