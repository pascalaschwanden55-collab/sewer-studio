using AuswertungPro.Next.Application.Ai.Evaluation;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProtocolMatchStateResetter
{
    public static CodingMatchRouting? Reset(IDictionary<Guid, CodingProtocolMatchBucket> buckets)
    {
        ArgumentNullException.ThrowIfNull(buckets);

        buckets.Clear();
        return null;
    }
}
