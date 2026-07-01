using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingBatchImportCaseUiSink
{
    public TrainingBatchImportCaseUiSink(
        Action<TrainingBatchImportLivePreview> updateLivePreview,
        Action<Action> invokeOnUi,
        Action<SelfTrainingEntryResult> addResult,
        Action<string, MatchLevel> updateCodeDistribution,
        Action<int> setSampleCount,
        Action<int> setCodesCovered,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(updateLivePreview);
        ArgumentNullException.ThrowIfNull(invokeOnUi);
        ArgumentNullException.ThrowIfNull(addResult);
        ArgumentNullException.ThrowIfNull(updateCodeDistribution);
        ArgumentNullException.ThrowIfNull(setSampleCount);
        ArgumentNullException.ThrowIfNull(setCodesCovered);
        ArgumentNullException.ThrowIfNull(log);

        UpdateLivePreview = updateLivePreview;
        InvokeOnUi = invokeOnUi;
        AddResult = addResult;
        UpdateCodeDistribution = updateCodeDistribution;
        SetSampleCount = setSampleCount;
        SetCodesCovered = setCodesCovered;
        Log = log;
    }

    public Action<TrainingBatchImportLivePreview> UpdateLivePreview { get; }

    public Action<Action> InvokeOnUi { get; }

    public Action<SelfTrainingEntryResult> AddResult { get; }

    public Action<string, MatchLevel> UpdateCodeDistribution { get; }

    public Action<int> SetSampleCount { get; }

    public Action<int> SetCodesCovered { get; }

    public Action<string> Log { get; }
}
