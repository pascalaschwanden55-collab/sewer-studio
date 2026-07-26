using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

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
    public async Task SaveAndIndexAsync_does_not_index_approved_sample_without_gold_frame()
    {
        var indexed = false;
        var sample = Sample(TrainingSampleStatus.Approved);
        sample.FramePath = string.Empty;
        var persister = new CodingTrainingSamplePersister(
            _ => Task.CompletedTask,
            _ =>
            {
                indexed = true;
                return Task.CompletedTask;
            });

        await persister.SaveAndIndexAsync(sample);

        Assert.False(indexed);
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

    [Fact]
    public async Task SaveAndIndexAsync_Codekorrektur_ersetzt_JSON_vollstaendig_und_indexiert_genau_diesen_Stand()
    {
        using var temp = new TempDir();
        var store = new TrainingSampleFileStore(Path.Combine(temp.Path, "training_samples.json"));
        var emptyEval = Path.Combine(temp.Path, "eval-disabled-for-test");
        store.ConfigureEvalProtection(emptyEval);
        await store.SaveAsync([
            new TrainingSample
            {
                SampleId = "sample-1",
                CaseId = "H-100",
                Code = "BAB",
                Beschreibung = "Alter Riss",
                Signature = "H-100|BAB|1.0|1.0",
                Status = TrainingSampleStatus.Approved
            }
        ]);
        TrainingSample? indexed = null;
        var corrected = new TrainingSample
        {
            SampleId = "sample-1",
            CaseId = "H-100",
            Code = "BBA",
            Beschreibung = "Korrigierter Wurzeleinwuchs",
            Signature = "H-100|BBA|1.0|1.0|b:0.500,0.500,0.200,0.200",
            Status = TrainingSampleStatus.Approved,
            FramePath = "gold.png"
        };
        var persister = new CodingTrainingSamplePersister(
            store,
            sample =>
            {
                indexed = sample;
                return Task.CompletedTask;
            });

        await persister.SaveAndIndexAsync(corrected);

        var persisted = Assert.Single(await store.LoadAsync());
        Assert.Equal("sample-1", persisted.SampleId);
        Assert.Equal("BBA", persisted.Code);
        Assert.Equal(corrected.Signature, persisted.Signature);
        Assert.Same(corrected, indexed);
    }

    [Fact]
    public async Task SaveAndIndexAsync_gleiche_Signatur_mit_anderer_Id_indexiert_keine_Waise()
    {
        using var temp = new TempDir();
        var store = new TrainingSampleFileStore(Path.Combine(temp.Path, "training_samples.json"));
        var emptyEval = Path.Combine(temp.Path, "eval-disabled-for-test");
        store.ConfigureEvalProtection(emptyEval);
        await store.SaveAsync([
            new TrainingSample
            {
                SampleId = "bestehend",
                CaseId = "H-100",
                Code = "BAB",
                Beschreibung = "Bestehender Riss",
                Signature = "H-100|BAB|1.0|1.0",
                Status = TrainingSampleStatus.Approved
            }
        ]);
        var indexed = false;
        var persister = new CodingTrainingSamplePersister(
            store,
            _ =>
            {
                indexed = true;
                return Task.CompletedTask;
            });
        var duplicate = new TrainingSample
        {
            SampleId = "neu",
            CaseId = "H-100",
            Code = "BAB",
            Beschreibung = "Doppelte Wahrheit",
            Signature = "H-100|BAB|1.0|1.0",
            Status = TrainingSampleStatus.Approved,
            FramePath = "gold.png"
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => persister.SaveAndIndexAsync(duplicate));

        Assert.Contains("bereits", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(indexed);
        Assert.Equal("bestehend", Assert.Single(await store.LoadAsync()).SampleId);
    }

    private static TrainingSample Sample(TrainingSampleStatus status, string id = "sample")
        => new()
        {
            SampleId = id,
            Status = status,
            Code = "BBA",
            CaseId = "H-100",
            FramePath = "gold.png"
        };

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "coding-persister-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
