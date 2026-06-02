using System.Collections.Generic;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Thin Wrapper: delegiert ITrainingSampleStore an den statischen TrainingSamplesStore.
/// Ermoeglicht testbare Abhaengigkeitsinjektion ohne die statische Klasse zu veraendern.
/// </summary>
public sealed class TrainingSamplesStoreAdapter : ITrainingSampleStore
{
    /// <inheritdoc />
    public Task<List<TrainingSample>> LoadAsync() =>
        TrainingSamplesStore.LoadAsync();

    /// <inheritdoc />
    public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples) =>
        TrainingSamplesStore.MergeOrUpdateAsync(samples);

    /// <inheritdoc />
    public Task MergeAndSaveAsync(List<TrainingSample> samples) =>
        TrainingSamplesStore.MergeAndSaveAsync(samples);
}
