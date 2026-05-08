using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Roadmap P1.3: Provenance fuer Trainings-/Export-Runs. Atomar persistierter
/// Verlauf (Begin/Complete/Fail/Cancel) mit Cap auf 200 Eintraege.
///
/// Tests laufen gegen einen frischen Temp-KnowledgeRoot, damit Produktiv-
/// Daten und andere Tests nicht stoeren.
/// </summary>
public sealed class TrainingRunsStoreTests : IDisposable
{
    private readonly KnowledgeRootScope _scope;

    public TrainingRunsStoreTests()
    {
        _scope = KnowledgeRootScope.Fresh();
    }

    public void Dispose() => _scope.Dispose();

    [Fact]
    public async Task BeginRunAsync_ProducesUniqueRunId_AndPersistsAsRunning()
    {
        var run1 = await TrainingRunsStore.BeginRunAsync(TrainingRunTriggers.SelfTraining);
        var run2 = await TrainingRunsStore.BeginRunAsync(TrainingRunTriggers.YoloRetrain);

        Assert.NotEqual(run1.RunId, run2.RunId);
        Assert.Equal(TrainingRunStatus.Running, run1.Status);
        Assert.Null(run1.FinishedUtc);

        var loaded = await TrainingRunsStore.LoadAsync();
        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, r => r.RunId == run1.RunId);
        Assert.Contains(loaded, r => r.RunId == run2.RunId);
    }

    [Fact]
    public async Task CompleteRunAsync_SetsSucceededAndFinishedUtc()
    {
        var run = await TrainingRunsStore.BeginRunAsync(TrainingRunTriggers.OperateurAnnotation);
        await TrainingRunsStore.CompleteRunAsync(run.RunId, samplesAffected: 17);

        var loaded = await TrainingRunsStore.LoadAsync();
        var saved = loaded.Single(r => r.RunId == run.RunId);

        Assert.Equal(TrainingRunStatus.Succeeded, saved.Status);
        Assert.NotNull(saved.FinishedUtc);
        Assert.Equal(17, saved.SamplesAffected);
        Assert.Null(saved.ErrorMessage);
    }

    [Fact]
    public async Task FailRunAsync_SetsFailedAndErrorMessage()
    {
        var run = await TrainingRunsStore.BeginRunAsync(TrainingRunTriggers.YoloDatasetExport);
        await TrainingRunsStore.FailRunAsync(run.RunId, "Disk full");

        var saved = (await TrainingRunsStore.LoadAsync()).Single(r => r.RunId == run.RunId);
        Assert.Equal(TrainingRunStatus.Failed, saved.Status);
        Assert.Equal("Disk full", saved.ErrorMessage);
    }

    [Fact]
    public async Task CancelRunAsync_SetsCancelled()
    {
        var run = await TrainingRunsStore.BeginRunAsync(TrainingRunTriggers.Manual);
        await TrainingRunsStore.CancelRunAsync(run.RunId, "user aborted");

        var saved = (await TrainingRunsStore.LoadAsync()).Single(r => r.RunId == run.RunId);
        Assert.Equal(TrainingRunStatus.Cancelled, saved.Status);
        Assert.Equal("user aborted", saved.Notes);
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownRunId_NoOp()
    {
        await TrainingRunsStore.CompleteRunAsync("does-not-exist");
        var loaded = await TrainingRunsStore.LoadAsync();
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task BeginRunAsync_EmptyTrigger_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            TrainingRunsStore.BeginRunAsync(""));
    }

    [Fact]
    public async Task TrimToMaxRetained_DropsOldestRuns()
    {
        // 205 Runs anlegen — Cap ist 200, also faellt 5 hinten ab.
        // Das Test-Setup ist bewusst >200 damit der Trim wirklich greift.
        for (int i = 0; i < 205; i++)
            await TrainingRunsStore.BeginRunAsync(TrainingRunTriggers.SelfTraining, notes: $"r{i}");

        var loaded = await TrainingRunsStore.LoadAsync();
        Assert.Equal(200, loaded.Count);
        // Die juengsten 5 muessen drin sein, der erste sollte raus sein.
        Assert.Contains(loaded, r => r.Notes == "r204");
        Assert.DoesNotContain(loaded, r => r.Notes == "r0");
    }

    [Fact]
    public void TrainingSample_TrainingRunId_RoundTrips()
    {
        // Provenance-Feld am TrainingSample ist bidirektional erreichbar.
        var sample = new TrainingSample { TrainingRunId = "abc-123" };
        Assert.Equal("abc-123", sample.TrainingRunId);

        sample.TrainingRunId = null;
        Assert.Null(sample.TrainingRunId);
    }

    /// <summary>
    /// Lenkt KnowledgeRootProvider auf einen Temp-Pfad und stellt nach
    /// Dispose den Vor-Zustand exakt wieder her.
    /// </summary>
    private sealed class KnowledgeRootScope : IDisposable
    {
        public string Root { get; }
        private readonly Func<string>? _previousResolver;

        private KnowledgeRootScope(string root, Func<string>? previousResolver)
        {
            Root = root;
            _previousResolver = previousResolver;
        }

        public static KnowledgeRootScope Fresh()
        {
            var prev = ReadResolver();
            var root = Path.Combine(Path.GetTempPath(),
                "TrainingRunsStoreTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            KnowledgeRootProvider.SetResolver(() => root);
            return new KnowledgeRootScope(root, prev);
        }

        public void Dispose()
        {
            WriteResolver(_previousResolver);
            try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
            catch { /* best-effort */ }
        }

        private static FieldInfo Field()
            => typeof(KnowledgeRootProvider).GetField("_resolver",
                BindingFlags.NonPublic | BindingFlags.Static)
               ?? throw new InvalidOperationException("KnowledgeRootProvider._resolver missing");

        private static Func<string>? ReadResolver()
            => (Func<string>?)Field().GetValue(null);

        private static void WriteResolver(Func<string>? r)
            => Field().SetValue(null, r);
    }
}
