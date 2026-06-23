using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingProtocolMatchRunner
{
    public static CodingMatchRouting Run(
        IEnumerable<CodingEvent> importEvents,
        IEnumerable<CodingEvent> codingEvents,
        IDictionary<Guid, CodingProtocolMatchBucket> buckets)
    {
        ArgumentNullException.ThrowIfNull(importEvents);
        ArgumentNullException.ThrowIfNull(codingEvents);
        ArgumentNullException.ThrowIfNull(buckets);

        var routing = CodingProtocolMatchService.Match(
            importEvents.Select(ev => ev.Entry).ToList(),
            codingEvents.Select(ev => ev.Entry).ToList());

        CodingProtocolMatchBucketBuilder.Rebuild(buckets, routing);
        return routing;
    }
}
