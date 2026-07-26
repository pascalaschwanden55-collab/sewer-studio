using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolMatchStateControllerTests
{
    [Fact]
    public void Store_updates_last_match()
    {
        var state = new CodingProtocolMatchStateController();
        var routing = Routing();

        state.Store(routing);

        Assert.Same(routing, state.LastMatch);
    }

    [Fact]
    public void Reset_clears_last_match_and_buckets()
    {
        var entryId = Guid.NewGuid();
        var state = new CodingProtocolMatchStateController();
        state.Store(Routing());
        state.Buckets[entryId] = CodingProtocolMatchBucket.TrainingGreen;

        var routing = state.Reset();

        Assert.Null(routing);
        Assert.Null(state.LastMatch);
        Assert.Empty(state.Buckets);
    }

    [Fact]
    public void TryGetBucket_reads_current_bucket_map()
    {
        var entryId = Guid.NewGuid();
        var state = new CodingProtocolMatchStateController();
        state.Buckets[entryId] = CodingProtocolMatchBucket.ReviewYellow;

        var found = state.TryGetBucket(entryId, out var bucket);

        Assert.True(found);
        Assert.Equal(CodingProtocolMatchBucket.ReviewYellow, bucket);
    }

    private static CodingMatchRouting Routing()
        => new(new BefundMatchResult(), [], [], [], [], []);
}
