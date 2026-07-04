using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingSamplePersistenceWorkflowRequest(
    IReadOnlyList<TrainingSample> Samples,
    TrainingSample? ChangedSample,
    Func<IReadOnlyList<TrainingSample>, Task> MergeOrUpdateAsync,
    Func<IReadOnlyList<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> IndexAsync,
    CancellationToken CancellationToken);

public sealed record TrainingSamplePersistenceRequestFactoryDefaults(
    Func<IEnumerable<TrainingSample>, Task> MergeOrUpdateAsync);

public static class TrainingSamplePersistenceRequestFactory
{
    public static TrainingSamplePersistenceWorkflowRequest CreateWithDefaults(
        IEnumerable<TrainingSample> samples,
        TrainingSample? changedSample,
        Func<List<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> indexAsync,
        CancellationToken cancellationToken = default)
        => Create(
            samples,
            changedSample,
            indexAsync,
            new TrainingSamplePersistenceRequestFactoryDefaults(
                MergeOrUpdateAsync: TrainingSamplesStore.MergeOrUpdateAsync),
            cancellationToken);

    public static TrainingSamplePersistenceWorkflowRequest Create(
        IEnumerable<TrainingSample> samples,
        TrainingSample? changedSample,
        Func<List<TrainingSample>, CancellationToken, Task<KbIndexOutcome>> indexAsync,
        TrainingSamplePersistenceRequestFactoryDefaults defaults,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(indexAsync);
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(defaults.MergeOrUpdateAsync);

        var snapshot = samples.ToList();
        return new TrainingSamplePersistenceWorkflowRequest(
            snapshot,
            changedSample,
            batch => defaults.MergeOrUpdateAsync(batch),
            (batch, token) => indexAsync(batch.ToList(), token),
            cancellationToken);
    }
}
