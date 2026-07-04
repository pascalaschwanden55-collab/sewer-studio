using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSamplePersistenceRequestFactoryTests
{
    [Fact]
    public async Task Create_erstellt_snapshot_und_adaptiert_store_und_index_delegates()
    {
        var first = Sample("first");
        var second = Sample("second");
        var samples = new ObservableCollection<TrainingSample> { first };
        var cts = new CancellationTokenSource();
        var calls = new List<string>();
        List<TrainingSample>? indexBatch = null;

        var request = TrainingSamplePersistenceRequestFactory.Create(
            samples,
            changedSample: first,
            indexAsync: (batch, token) =>
            {
                indexBatch = batch;
                Assert.Equal(cts.Token, token);
                calls.Add("index:" + string.Join(",", batch.Select(s => s.SampleId)));
                return Task.FromResult(KbIndexOutcome.Empty);
            },
            defaults: new TrainingSamplePersistenceRequestFactoryDefaults(
                MergeOrUpdateAsync: batch =>
                {
                    calls.Add("merge:" + string.Join(",", batch.Select(s => s.SampleId)));
                    return Task.CompletedTask;
                }),
            cts.Token);

        samples.Add(second);

        Assert.Equal([first], request.Samples);
        Assert.Same(first, request.ChangedSample);
        Assert.Equal(cts.Token, request.CancellationToken);

        await request.MergeOrUpdateAsync(request.Samples);
        await request.IndexAsync(request.Samples, cts.Token);

        Assert.IsType<List<TrainingSample>>(indexBatch);
        Assert.Equal(["merge:first", "index:first"], calls);
    }

    private static TrainingSample Sample(string id)
        => new()
        {
            SampleId = id,
            CaseId = "case-" + id,
            Code = "BAB",
            Status = TrainingSampleStatus.New
        };
}
