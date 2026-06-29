using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSamplePersistenceWorkflowControllerTests
{
    [Fact]
    public async Task PersistAsync_without_changed_sample_merges_all_samples_without_indexing()
    {
        var samples = new[] { Sample("a"), Sample("b") };
        var savedBatches = new List<IReadOnlyList<string>>();
        var indexCalls = 0;

        await TrainingSamplePersistenceWorkflowController.PersistAsync(
            samples,
            changedSample: null,
            mergeOrUpdateAsync: batch =>
            {
                savedBatches.Add(batch.Select(s => s.SampleId).ToList());
                return Task.CompletedTask;
            },
            indexAsync: (_, _) =>
            {
                indexCalls++;
                return Task.FromResult(KbIndexOutcome.Empty);
            });

        var saved = Assert.Single(savedBatches);
        Assert.Equal(["a", "b"], saved);
        Assert.Equal(0, indexCalls);
    }

    [Fact]
    public async Task PersistAsync_for_approved_sample_persists_pending_then_indexed_state()
    {
        var sample = Sample("approved");
        sample.Status = TrainingSampleStatus.Approved;
        var persistedStates = new List<KbIndexState>();

        await TrainingSamplePersistenceWorkflowController.PersistAsync(
            [sample],
            sample,
            mergeOrUpdateAsync: batch =>
            {
                persistedStates.Add(Assert.Single(batch).KbIndexState);
                return Task.CompletedTask;
            },
            indexAsync: (batch, _) =>
            {
                Assert.Equal("approved", Assert.Single(batch).SampleId);
                return Task.FromResult(new KbIndexOutcome(["approved"], []));
            });

        Assert.Equal([KbIndexState.None, KbIndexState.Pending, KbIndexState.Indexed], persistedStates);
        Assert.Equal(KbIndexState.Indexed, sample.KbIndexState);
    }

    [Theory]
    [InlineData(true, KbIndexState.Skipped)]
    [InlineData(false, KbIndexState.Error)]
    public async Task PersistAsync_maps_non_indexed_outcomes_to_skipped_or_error(bool skipped, KbIndexState expected)
    {
        var sample = Sample("approved");
        sample.Status = TrainingSampleStatus.Approved;

        await TrainingSamplePersistenceWorkflowController.PersistAsync(
            [sample],
            sample,
            mergeOrUpdateAsync: _ => Task.CompletedTask,
            indexAsync: (_, _) => Task.FromResult(new KbIndexOutcome([], skipped ? ["approved"] : [])));

        Assert.Equal(expected, sample.KbIndexState);
    }

    private static TrainingSample Sample(string id)
        => new()
        {
            SampleId = id,
            CaseId = "case",
            Code = "BAA",
            FramePath = "frame.jpg"
        };
}
