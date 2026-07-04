using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingSamplePersistenceWorkflowController
{
    public static Task PersistAsync(TrainingSamplePersistenceWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return PersistAsync(
            request.Samples,
            request.ChangedSample,
            request.MergeOrUpdateAsync,
            request.IndexAsync,
            request.CancellationToken);
    }

    public static async Task PersistAsync(
        IReadOnlyList<TrainingSample> samples,
        TrainingSample? changedSample,
        Func<IReadOnlyList<TrainingSample>, Task> mergeOrUpdateAsync,
        Func<IReadOnlyList<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> indexAsync,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(mergeOrUpdateAsync);
        ArgumentNullException.ThrowIfNull(indexAsync);

        if (changedSample is not null)
            await mergeOrUpdateAsync(new[] { changedSample }).ConfigureAwait(false);
        else
            await mergeOrUpdateAsync(samples).ConfigureAwait(false);

        if (changedSample?.Status != TrainingSampleStatus.Approved)
            return;

        changedSample.KbIndexState = KbIndexState.Pending;
        await mergeOrUpdateAsync(new[] { changedSample }).ConfigureAwait(false);

        var changedSamples = new[] { changedSample };
        var outcome = await indexAsync(changedSamples, ct).ConfigureAwait(false);
        changedSample.KbIndexState = outcome.IsIndexed(changedSample.SampleId)
            ? KbIndexState.Indexed
            : outcome.IsSkipped(changedSample.SampleId)
                ? KbIndexState.Skipped
                : KbIndexState.Error;

        await mergeOrUpdateAsync(changedSamples).ConfigureAwait(false);
    }
}
