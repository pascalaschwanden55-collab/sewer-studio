using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTrainingSamplePersisterTests
{
    [Fact]
    public async Task SaveAndIndexAsync_saves_samples_once()
    {
        var savedBatches = new List<List<TrainingSample>>();
        var persister = new CodingTrainingSamplePersister(
            samples =>
            {
                savedBatches.Add(samples);
                return Task.CompletedTask;
            });
        var approved = Sample(TrainingSampleStatus.Approved);
        var rejected = Sample(TrainingSampleStatus.Rejected);

        await persister.SaveAndIndexAsync(new[] { approved, rejected });

        Assert.Single(savedBatches);
        Assert.Equal(new[] { approved, rejected }, savedBatches[0]);
    }

    [Fact]
    public async Task SaveAndIndexAsync_indexes_only_approved_samples_after_save()
    {
        var calls = new List<string>();
        var approved = Sample(TrainingSampleStatus.Approved, "approved");
        var rejected = Sample(TrainingSampleStatus.Rejected, "rejected");
        var pending = Sample(TrainingSampleStatus.New, "new");
        var persister = new CodingTrainingSamplePersister(
            _ =>
            {
                calls.Add("save");
                return Task.CompletedTask;
            },
            sample =>
            {
                calls.Add($"index:{sample.SampleId}");
                return Task.CompletedTask;
            });

        await persister.SaveAndIndexAsync(new[] { rejected, approved, pending });

        Assert.Equal(new[] { "save", "index:approved" }, calls);
    }

    [Fact]
    public async Task SaveAndIndexAsync_does_nothing_for_empty_batch()
    {
        var saved = false;
        var persister = new CodingTrainingSamplePersister(
            _ =>
            {
                saved = true;
                return Task.CompletedTask;
            },
            _ => throw new InvalidOperationException("Indexing must not run."));

        await persister.SaveAndIndexAsync(Array.Empty<TrainingSample>());

        Assert.False(saved);
    }

    private static TrainingSample Sample(TrainingSampleStatus status, string id = "sample")
        => new()
        {
            SampleId = id,
            Status = status,
            Code = "BBA",
            CaseId = "H-100"
        };
}
