using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingProtocolMatchStateController
{
    private readonly Dictionary<Guid, CodingProtocolMatchBucket> _buckets = new();

    public CodingMatchRouting? LastMatch { get; private set; }

    public IDictionary<Guid, CodingProtocolMatchBucket> Buckets => _buckets;

    public void Store(CodingMatchRouting routing)
    {
        ArgumentNullException.ThrowIfNull(routing);

        LastMatch = routing;
    }

    public CodingMatchRouting? Reset()
    {
        LastMatch = CodingProtocolMatchStateResetter.Reset(_buckets);
        return LastMatch;
    }

    public bool TryGetBucket(Guid entryId, out CodingProtocolMatchBucket bucket)
        => _buckets.TryGetValue(entryId, out bucket);
}
