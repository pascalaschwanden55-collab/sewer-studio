using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolMatchBucketBuilderTests
{
    [Fact]
    public void Build_maps_pair_and_single_buckets_to_entry_ids()
    {
        var greenGt = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var greenKi = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var yellowGt = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var wrongKi = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var missed = Guid.Parse("00000000-0000-0000-0000-000000000005");
        var extra = Guid.Parse("00000000-0000-0000-0000-000000000006");

        var routing = Routing(
            green: [Pair(greenGt, greenKi)],
            yellow: [Pair(yellowGt, Guid.Parse("00000000-0000-0000-0000-000000000007"))],
            wrong: [Pair(Guid.Parse("00000000-0000-0000-0000-000000000008"), wrongKi)],
            missed: [Finding(missed)],
            extra: [Finding(extra)]);

        var buckets = CodingProtocolMatchBucketBuilder.Build(routing);

        Assert.Equal(CodingProtocolMatchBucket.TrainingGreen, buckets[greenGt]);
        Assert.Equal(CodingProtocolMatchBucket.TrainingGreen, buckets[greenKi]);
        Assert.Equal(CodingProtocolMatchBucket.ReviewYellow, buckets[yellowGt]);
        Assert.Equal(CodingProtocolMatchBucket.WrongCode, buckets[wrongKi]);
        Assert.Equal(CodingProtocolMatchBucket.Missed, buckets[missed]);
        Assert.Equal(CodingProtocolMatchBucket.FalseAlarm, buckets[extra]);
    }

    [Fact]
    public void Build_ignores_invalid_reference_ids()
    {
        var routing = Routing(
            green: [new BefundMatchPair(Finding("bad"), Finding("also-bad"), 0.0, "gruen")],
            yellow: [],
            wrong: [],
            missed: [Finding("not-a-guid")],
            extra: [Finding(null)]);

        var buckets = CodingProtocolMatchBucketBuilder.Build(routing);

        Assert.Empty(buckets);
    }

    [Fact]
    public void Rebuild_clears_existing_target_before_copying_new_buckets()
    {
        var oldId = Guid.Parse("00000000-0000-0000-0000-000000000099");
        var newId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var target = new Dictionary<Guid, CodingProtocolMatchBucket>
        {
            [oldId] = CodingProtocolMatchBucket.FalseAlarm
        };

        CodingProtocolMatchBucketBuilder.Rebuild(target, Routing(
            green: [],
            yellow: [],
            wrong: [],
            missed: [Finding(newId)],
            extra: []));

        Assert.False(target.ContainsKey(oldId));
        Assert.Equal(CodingProtocolMatchBucket.Missed, target[newId]);
    }

    private static CodingMatchRouting Routing(
        IReadOnlyList<BefundMatchPair> green,
        IReadOnlyList<BefundMatchPair> yellow,
        IReadOnlyList<BefundMatchPair> wrong,
        IReadOnlyList<BefundMatchFinding> missed,
        IReadOnlyList<BefundMatchFinding> extra)
        => new(new BefundMatchResult(), green, yellow, wrong, missed, extra);

    private static BefundMatchPair Pair(Guid gt, Guid ki)
        => new(Finding(gt), Finding(ki), 0.0, "gruen");

    private static BefundMatchFinding Finding(Guid refId)
        => Finding(refId.ToString());

    private static BefundMatchFinding Finding(string? refId)
        => new("BAB", 1.0, 1.0, "Riss", refId);
}
