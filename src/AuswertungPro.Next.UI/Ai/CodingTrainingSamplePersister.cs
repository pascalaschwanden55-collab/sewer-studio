using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingTrainingSamplePersister
{
    private readonly Func<List<TrainingSample>, Task> _mergeAndSaveAsync;
    private readonly Func<TrainingSample, Task>? _indexConfirmedSampleAsync;

    public CodingTrainingSamplePersister(Func<ICodingSessionService?> sessionProvider)
        : this(
            TrainingSamplesStore.MergeAndSaveAsync,
            CreateIndexConfirmedSampleAsync(sessionProvider))
    {
    }

    public CodingTrainingSamplePersister(
        Func<List<TrainingSample>, Task> mergeAndSaveAsync,
        Func<TrainingSample, Task>? indexConfirmedSampleAsync = null)
    {
        _mergeAndSaveAsync = mergeAndSaveAsync ?? throw new ArgumentNullException(nameof(mergeAndSaveAsync));
        _indexConfirmedSampleAsync = indexConfirmedSampleAsync;
    }

    public Task SaveAndIndexAsync(TrainingSample sample)
        => SaveAndIndexAsync(new List<TrainingSample> { sample });

    public async Task SaveAndIndexAsync(IReadOnlyCollection<TrainingSample> samples)
    {
        if (samples.Count == 0)
            return;

        var sampleList = samples as List<TrainingSample> ?? samples.ToList();
        await _mergeAndSaveAsync(sampleList);

        if (_indexConfirmedSampleAsync is null)
            return;

        foreach (var sample in sampleList)
        {
            if (sample.Status == TrainingSampleStatus.Approved)
                await _indexConfirmedSampleAsync(sample);
        }
    }

    private static Func<TrainingSample, Task> CreateIndexConfirmedSampleAsync(
        Func<ICodingSessionService?> sessionProvider)
    {
        ArgumentNullException.ThrowIfNull(sessionProvider);
        return sample => sessionProvider()?.IndexConfirmedSampleAsync(sample) ?? Task.CompletedTask;
    }
}
