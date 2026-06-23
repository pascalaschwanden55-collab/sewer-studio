using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolMatchStateResetterTests
{
    [Fact]
    public void Reset_clears_match_buckets_and_returns_no_routing()
    {
        var buckets = new Dictionary<Guid, CodingProtocolMatchBucket>
        {
            [Guid.NewGuid()] = CodingProtocolMatchBucket.TrainingGreen,
            [Guid.NewGuid()] = CodingProtocolMatchBucket.FalseAlarm
        };

        var routing = CodingProtocolMatchStateResetter.Reset(buckets);

        Assert.Null(routing);
        Assert.Empty(buckets);
    }
}
