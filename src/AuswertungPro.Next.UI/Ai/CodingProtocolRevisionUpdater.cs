using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingProtocolRevisionUpdater
{
    public static int ApplyCodingEvents(ProtocolRevision revision, IEnumerable<CodingEvent> events)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(events);

        revision.Entries ??= new List<ProtocolEntry>();

        var eventEntries = events
            .Select(ev => ev.Entry)
            .Where(e => !string.IsNullOrWhiteSpace(e.Code))
            .GroupBy(e => e.EntryId)
            .Select(g => g.Last())
            .ToDictionary(e => e.EntryId, e => e);

        var existingById = revision.Entries.ToDictionary(e => e.EntryId, e => e);
        foreach (var existing in revision.Entries)
        {
            if (eventEntries.TryGetValue(existing.EntryId, out var updated))
            {
                CodingProtocolEntryCopier.CopyValues(updated, existing);
                existing.IsDeleted = false;
            }
            else
            {
                existing.IsDeleted = true;
            }
        }

        foreach (var kv in eventEntries)
        {
            if (!existingById.ContainsKey(kv.Key))
                revision.Entries.Add(kv.Value);
        }

        return eventEntries.Count;
    }
}
