using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Thin Wrapper: delegiert ITrainingSampleStore an den statischen TrainingSamplesStore.
/// Ermoeglicht testbare Abhaengigkeitsinjektion ohne die statische Klasse zu veraendern.
/// </summary>
public sealed class TrainingSamplesStoreAdapter : ITrainingSampleStore
{
    private readonly ITrainingSampleStore _store;

    public TrainingSamplesStoreAdapter()
        : this(TrainingSamplesStore.Current)
    {
    }

    public TrainingSamplesStoreAdapter(ITrainingSampleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public Task<List<TrainingSample>> LoadAsync() =>
        _store.LoadAsync();

    /// <inheritdoc />
    public Task SaveAsync(List<TrainingSample> samples) =>
        _store.SaveAsync(samples);

    /// <inheritdoc />
    public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples) =>
        _store.MergeOrUpdateAsync(samples);

    /// <inheritdoc />
    public Task MergeAndSaveAsync(List<TrainingSample> samples) =>
        _store.MergeAndSaveAsync(samples);

    /// <inheritdoc />
    public Task<bool> TryAddNewAsync(TrainingSample sample, CancellationToken ct = default) =>
        _store.TryAddNewAsync(sample, ct);

    /// <inheritdoc />
    public Task<bool> RemoveBySampleIdAsync(string sampleId) =>
        _store.RemoveBySampleIdAsync(sampleId);

    /// <inheritdoc />
    public Task<bool> ReplaceBySampleIdAsync(TrainingSample sample) =>
        _store.ReplaceBySampleIdAsync(sample);
}
