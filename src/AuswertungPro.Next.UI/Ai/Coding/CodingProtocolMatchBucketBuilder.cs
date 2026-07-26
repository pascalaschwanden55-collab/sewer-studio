using AuswertungPro.Next.Application.Ai.Evaluation;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProtocolMatchBucketBuilder
{
    public static Dictionary<Guid, CodingProtocolMatchBucket> Build(CodingMatchRouting routing)
    {
        var buckets = new Dictionary<Guid, CodingProtocolMatchBucket>();

        foreach (var pair in routing.Trainingskandidaten)
            AddPairBuckets(buckets, pair, CodingProtocolMatchBucket.TrainingGreen);

        foreach (var pair in routing.ReviewGelb)
            AddPairBuckets(buckets, pair, CodingProtocolMatchBucket.ReviewYellow);

        foreach (var pair in routing.FalscherCodeReview)
            AddPairBuckets(buckets, pair, CodingProtocolMatchBucket.WrongCode);

        foreach (var missed in routing.Verpasst)
            if (Guid.TryParse(missed.RefId, out var missedId))
                buckets[missedId] = CodingProtocolMatchBucket.Missed;

        foreach (var extra in routing.Fehlalarm)
            if (Guid.TryParse(extra.RefId, out var extraId))
                buckets[extraId] = CodingProtocolMatchBucket.FalseAlarm;

        return buckets;
    }

    public static void Rebuild(IDictionary<Guid, CodingProtocolMatchBucket> target, CodingMatchRouting routing)
    {
        target.Clear();
        foreach (var (entryId, bucket) in Build(routing))
            target[entryId] = bucket;
    }

    private static void AddPairBuckets(
        IDictionary<Guid, CodingProtocolMatchBucket> buckets,
        BefundMatchPair pair,
        CodingProtocolMatchBucket bucket)
    {
        if (Guid.TryParse(pair.Gt.RefId, out var gtId))
            buckets[gtId] = bucket;

        if (Guid.TryParse(pair.Ki.RefId, out var kiId))
            buckets[kiId] = bucket;
    }
}
