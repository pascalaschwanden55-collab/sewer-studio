using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingProtocolTrainingCandidateResolver
{
    public static IReadOnlyList<CodingEvent> ResolveImportEvents(
        IEnumerable<BefundMatchPair> trainingCandidates,
        IEnumerable<CodingEvent> importEvents)
    {
        var importEventsByEntryId = new Dictionary<Guid, CodingEvent>();
        foreach (var importEvent in importEvents)
        {
            importEventsByEntryId.TryAdd(importEvent.Entry.EntryId, importEvent);
        }

        var result = new List<CodingEvent>();
        foreach (var pair in trainingCandidates)
        {
            if (Guid.TryParse(pair.Gt.RefId, out var importEntryId)
                && importEventsByEntryId.TryGetValue(importEntryId, out var importEvent))
            {
                result.Add(importEvent);
            }
        }

        return result;
    }
}
