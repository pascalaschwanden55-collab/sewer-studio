using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingKbUpdateController
{
    public static bool ShouldRun(SelfTrainingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.ExactMatches > 0 && result.SamplesGenerated > 0;
    }

    public static List<TrainingSample> SelectApprovedSamplesForRun(
        IEnumerable<TrainingSample> samples,
        SelfTrainingResult result)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(result);

        return samples
            .Where(s => s.CaseId == result.CaseId
                && s.Status == TrainingSampleStatus.Approved)
            .ToList();
    }

    public static void MarkPendingBeforeIndex(IEnumerable<TrainingSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        foreach (var sample in samples.Where(s => s.KbIndexState is KbIndexState.None or KbIndexState.Error))
            sample.KbIndexState = KbIndexState.Pending;
    }

    public static void ApplyOutcome(
        IEnumerable<TrainingSample> samples,
        KbIndexOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(outcome);

        var indexedSet = outcome.IndexedIds.ToHashSet(StringComparer.Ordinal);
        foreach (var sample in samples)
        {
            sample.KbIndexState = indexedSet.Contains(sample.SampleId)
                ? KbIndexState.Indexed
                : outcome.SkippedIds.Contains(sample.SampleId)
                    ? KbIndexState.Skipped
                    : (sample.KbIndexState == KbIndexState.Pending ? KbIndexState.Error : sample.KbIndexState);
        }
    }

    public static string BuildStartLogMessage(int sampleCount)
        => $"{sampleCount} ExactMatch-Samples \u2014 starte KB-Update...";

    public static async Task RunApprovedSamplesUpdateAsync(
        SelfTrainingResult result,
        Func<Task<List<TrainingSample>>> loadSamplesAsync,
        Func<IEnumerable<TrainingSample>, Task> mergeOrUpdateAsync,
        Func<List<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> indexAsync,
        Action<string> log,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(loadSamplesAsync);
        ArgumentNullException.ThrowIfNull(mergeOrUpdateAsync);
        ArgumentNullException.ThrowIfNull(indexAsync);
        ArgumentNullException.ThrowIfNull(log);

        if (!ShouldRun(result))
            return;

        var allSamples = await loadSamplesAsync();
        var newApproved = SelectApprovedSamplesForRun(allSamples, result);
        if (newApproved.Count == 0)
            return;

        MarkPendingBeforeIndex(newApproved);
        await mergeOrUpdateAsync(newApproved);

        log(BuildStartLogMessage(newApproved.Count));
        var outcome = await indexAsync(newApproved, ct);
        ApplyOutcome(newApproved, outcome);
        await mergeOrUpdateAsync(newApproved);
    }
}
