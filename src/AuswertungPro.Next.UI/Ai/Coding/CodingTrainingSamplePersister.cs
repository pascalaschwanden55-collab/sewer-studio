using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingTrainingSamplePersister
{
    private readonly ITrainingSampleStore? _sampleStore;
    private readonly Func<List<TrainingSample>, Task>? _mergeAndSaveAsync;
    private readonly Func<TrainingSample, Task>? _indexConfirmedSampleAsync;

    public CodingTrainingSamplePersister(
        Func<ICodingSessionService?> sessionProvider,
        ITrainingSampleStore? trainingSamples = null)
        : this(
            trainingSamples ?? TrainingSamplesStore.Current,
            CreateIndexConfirmedSampleAsync(sessionProvider))
    {
    }

    /// <summary>
    /// Verbindet den kanonischen JSON-Speicher direkt mit der nachgelagerten
    /// Indexierung. Dadurch wird nur genau der Datensatz indexiert, der zuvor
    /// unter derselben SampleId vollstaendig gespeichert wurde.
    /// </summary>
    public CodingTrainingSamplePersister(
        ITrainingSampleStore sampleStore,
        Func<TrainingSample, Task>? indexConfirmedSampleAsync = null)
    {
        _sampleStore = sampleStore ?? throw new ArgumentNullException(nameof(sampleStore));
        _indexConfirmedSampleAsync = indexConfirmedSampleAsync;
    }

    /// <summary>
    /// Kompatibilitaets-Konstruktor fuer bestehende Aufrufer und fokussierte Tests.
    /// Neue Produktivaufrufer sollen den ITrainingSampleStore-Konstruktor verwenden.
    /// </summary>
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
        if (_sampleStore is null)
        {
            await _mergeAndSaveAsync!(sampleList).ConfigureAwait(false);

            if (_indexConfirmedSampleAsync is null)
                return;

            foreach (var sample in sampleList)
                await IndexIfEligibleAsync(sample).ConfigureAwait(false);
            return;
        }

        foreach (var sample in sampleList)
        {
            var replaced = await _sampleStore
                .ReplaceBySampleIdAsync(sample)
                .ConfigureAwait(false);
            if (!replaced)
            {
                var added = await _sampleStore
                    .TryAddNewAsync(sample)
                    .ConfigureAwait(false);
                if (!added)
                {
                    throw new InvalidOperationException(
                        $"Sample '{sample.SampleId}' wurde nicht gespeichert: Seine Signatur " +
                        "ist bereits einem anderen Gold-Datensatz zugeordnet.");
                }
            }

            // Erst NACH bestaetigter, exakter Speicherung weiterreichen. So koennen
            // SQLite/Teacher niemals eine neue Id indexieren, waehrend JSON wegen
            // Signatur-Dedup noch den alten Datensatz enthaelt.
            await IndexIfEligibleAsync(sample).ConfigureAwait(false);
        }
    }

    private Task IndexIfEligibleAsync(TrainingSample sample)
    {
        if (_indexConfirmedSampleAsync is null
            || sample.Status != TrainingSampleStatus.Approved
            || string.IsNullOrWhiteSpace(sample.FramePath))
            return Task.CompletedTask;

        return _indexConfirmedSampleAsync(sample);
    }

    private static Func<TrainingSample, Task> CreateIndexConfirmedSampleAsync(
        Func<ICodingSessionService?> sessionProvider)
    {
        ArgumentNullException.ThrowIfNull(sessionProvider);
        return sample => sessionProvider()?.IndexConfirmedSampleAsync(sample) ?? Task.CompletedTask;
    }
}
